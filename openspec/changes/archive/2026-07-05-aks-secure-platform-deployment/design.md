## Context

`azure-entra-auth-iac` stood up `infra/terraform/` as the identity/IAM root — Entra app registrations, RBAC role assignments — but treats AKS, Key Vault, and ACR as pre-existing `data` sources managed by hand, and never deploys a workload. There is no VNet today — the existing AKS cluster, Key Vault, and ACR are each reachable over public network paths with no private networking layer to peer anything into. Separately, `infra/helm/` has templates for `api`, `email-delivery`, and `ingest` only; `portal`, `crawler`, and `summarizer` have neither a Helm template nor a CI pipeline. Every existing CI workflow (`ingest.yml`, `email-delivery.yml`, `db-migrations.yml`) stops at `docker push` — nothing updates the cluster. The datastore is SQLite via a `schema.sql`-turned-DbUp runner (`db-migration-runner` capability), fine for v0.1 single-instance, but CLAUDE.md's own tech-stack table already earmarks Postgres once persistent-volume/multi-instance concerns apply — which they now do, since every service is about to run as a real K8s workload rather than a local process.

Critically: **nothing is in production yet.** There is no live traffic, no real subscriber data, and no existing VNet topology to retrofit around. This is the cheapest and only time to get the network foundation right the first time — including, if needed, recreating the AKS cluster with a network configuration (CNI plugin, subnet layout) that supports private endpoints and NetworkPolicy from day one, rather than bolting that on later against a running system.

This change is the "make it actually run in production, securely" pass: one AKS cluster hosting all six services inside a purpose-built VNet, Postgres as the shared datastore, Key Vault (and ACR) reachable only over private networking, and CI that both builds and deploys.

## Goals / Non-Goals

**Goals:**
- Every service (`api`, `crawler`, `summarizer`, `email-service`, `ingest`, `portal`, `db-migrations`) runs in the one existing AKS cluster, each with its own least-privilege identity.
- No plaintext secret ever exists in git, `values.yaml`, or a checked-in K8s `Secret` manifest — Key Vault via Workload Identity is the only path to a runtime credential.
- Key Vault, ACR, and Postgres are reachable only from inside the platform's own VNet — no public data-plane endpoint on any of the three.
- CI merge to `main` results in a running change in the cluster without a manual `helm upgrade` from someone's laptop.
- Move off SQLite to a Postgres datastore that survives pod rescheduling and supports multiple concurrent instances.

**Non-Goals:**
- ArgoCD / GitOps pull-based deployment — CI-driven push (`helm upgrade` in-workflow) is sufficient at this team size and service count; revisit only if a second cluster/environment is added and drift between "what's in git" and "what's running" becomes a real operational problem (pivot trigger, not a default).
- Multi-cluster / multi-region — one cluster is the explicit ask; no HA-across-regions design here.
- Azure Firewall, DDoS Protection Standard, or a hub-spoke VNet topology — a single-spoke VNet with NSGs and private endpoints is the right amount of network security for one team/one cluster/one environment; these are real upgrades but speculative at current scale (revisit if a compliance requirement or a second environment demands it).
- Re-litigating the Entra/RBAC identity model from `azure-entra-auth-iac` — this change consumes that foundation (same Terraform root) rather than replacing it.

## Decisions

**Decision: Stand up a dedicated VNet with a subnet per concern — AKS node subnet, Postgres delegated subnet, private-endpoints subnet — as the network foundation everything else attaches to**
- Why: Nothing exists today to peer into, so this is a from-scratch design, not a retrofit. A single flat VNet with purpose-specific subnets (rather than one shared subnet for everything) lets NSGs apply different rules per concern — e.g. the private-endpoints subnet only needs to accept traffic from the AKS subnet, never the reverse — and matches the delegation requirements Postgres Flexible Server's VNet-integrated deployment mode specifically requires (a subnet delegated to `Microsoft.DBforPostgreSQL/flexibleServers`, which cannot be shared with AKS nodes).
- Structure: `infra/terraform/network.tf` — one `azurerm_virtual_network`, `snet-aks`, `snet-postgres` (delegated), `snet-privatelink` (Key Vault + ACR private endpoints), plus a `privatelink.*` Private DNS Zone per privately-linked service (Key Vault, ACR, Postgres) linked to the VNet so in-cluster DNS resolves the private IP.
- Alternative considered: reuse a single subnet for AKS nodes and private endpoints — rejected; Postgres Flexible Server's delegated-subnet requirement rules this out outright, and mixing private endpoint NICs with node NICs in one subnet muddies NSG rules for no benefit.

**Decision: Verify the existing AKS cluster's network plugin now; recreate the cluster if it can't support private endpoints/NetworkPolicy, rather than retrofit later**
- Why: Azure CNI (not the legacy `kubenet` plugin) is required for pods to reach VNet-integrated private endpoints and for Calico/Azure NetworkPolicy enforcement — both load-bearing for this change's security goals. Changing an AKS cluster's network plugin after creation isn't supported in place; the only fix is recreating the cluster. Since nothing is in production, this is the one moment recreating the cluster costs nothing (no workload downtime to coordinate, no data to migrate off it).
- Action: confirm the current cluster's `network_plugin` via `az aks show` before writing `resources.tf`'s import block. If it's already Azure CNI with a VNet-attachable subnet, import as-is (cheapest path). If it's `kubenet` or has no usable subnet, provision a fresh AKS cluster inside the new `snet-aks` subnet with Azure CNI (Overlay mode — conserves VNet IP space, still supports private link egress and NetworkPolicy) and point Helm/CI at the new cluster; decommission the old one once cutover is confirmed.
- Alternative considered: keep the existing cluster regardless of plugin and route private-endpoint traffic through some other path (e.g. a NAT/UDR workaround) — rejected; adds a permanent networking workaround to dodge a one-time, currently-free fix.

**Decision: Azure Workload Identity (per-service User-Assigned Managed Identity + federated credential + Secrets Store CSI Driver), not node-level Managed Identity or Key Vault access policies**
- Why: Node-level identity gives every pod on a node the same Key Vault access — the summarizer pod could read the Stripe key. Workload Identity binds a K8s ServiceAccount (one per service, namespace-scoped) to its own Managed Identity via OIDC federation, so RBAC scoping is per-service, matching the least-privilege pattern `azure-entra-auth-iac` already applies to humans.
- Alternative considered: Key Vault access policies (legacy model) instead of RBAC — rejected; `azure-entra-auth-iac` already migrated the vault to RBAC mode for human access, mixing access-policy and RBAC modes on one vault is not supported.

**Decision: Key Vault and ACR get private endpoints too, with public network access disabled**
- Why: The proposal's ask to "leverage Key Vault to its fullest" is a network-security question as much as an RBAC one — Workload Identity narrows *who* can read a secret, but the vault is still reachable from the public internet unless told otherwise. Now that `snet-privatelink` exists for Postgres, extending it to Key Vault and ACR is marginal Terraform (one `azurerm_private_endpoint` + DNS zone link each) for a real reduction in attack surface, and — since nothing is in prod — there's no live client depending on the current public endpoint to break.
- Alternative considered: leave Key Vault/ACR on public access with IP allow-listing (firewall rules) instead of private endpoints — rejected; allow-lists need maintenance as CI runner IPs/dev egress IPs change, private endpoints don't.

**Decision: Azure Database for PostgreSQL Flexible Server, private-networked, not in-cluster Postgres**
- Why: Managed backups, point-in-time restore, and minor-version patching without operational burden; VNet integration via the delegated `snet-postgres` subnet (no public endpoint) keeps the DB reachable only from inside the platform VNet. This is a two-person team — running Postgres-as-a-StatefulSet means this team owns PVC provisioning, backup scheduling/testing, and failover, on top of everything else in this change. Cost-wise a Flexible Server Burstable tier (B1ms/B2s) at this data volume is single-digit dollars a month more than a comparable AKS-node PVC would cost in compute+storage once backup storage is counted — the "cheaper elsewhere" case doesn't actually favor in-cluster here.
- Alternative considered: in-cluster Postgres via a Helm chart (e.g. Bitnami/CloudNativePG) — genuinely viable and not rejected on cost grounds; rejected because it adds backup/HA/failover operational surface this team doesn't want to own for a marginal cost difference. Revisit if Flexible Server pricing changes materially at scale, or if a future requirement (e.g. wanting Postgres extensions Flexible Server doesn't support) forces the question.
- Connection auth: connection string (username/password) as a Key Vault secret for v1, resolved via Workload Identity — not Postgres Azure AD auth. Azure AD auth to Flexible Server is real but adds `Npgsql` token-refresh wiring; revisit as a hardening follow-up once the password-based path is proven, not a blocker for this change.

**Decision: CI-driven `helm upgrade` deploy job, gated by a required-reviewer GitHub Environment — not ArgoCD**
- Why: Mirrors the exact pattern `terraform-apply.yml` already uses (OIDC federated Azure login, `environment:` with required reviewers as the human checkpoint). One more `az aks get-credentials && helm upgrade` step per workflow is the smallest change that gets code running in the cluster; ArgoCD would add a second control plane (Argo's own CRDs, RBAC, and a sync-drift model) to reason about for a single cluster with six services.
- Alternative considered: ArgoCD now — deferred per proposal.md; the fast-follow path is straightforward (point Argo `Application` CRDs at the same `infra/helm` charts, no chart rewrite needed) if/when a second environment justifies GitOps.

**Decision: `crawler` and `db-migrations` run as Helm-templated CronJob/Job (not long-running Deployments); `api`, `summarizer`, `email-service`, `ingest`, `portal` as Deployments**
- Why: `crawler` runs on a schedule (matches its current standalone-process shape); `db-migrations` runs once per deploy as an init-style Job ahead of the `api` rollout (same "runner fails fast, pod never starts against a bad schema" contract `db-migration-runner`'s spec already establishes). The other five are always-on request/queue consumers.

**Decision: Single namespace `nomadrules-services` for app workloads; keep the existing `nomadrules-data` namespace for observability (Pushgateway/OTel), do not create a namespace per service**
- Why: One cluster was the explicit ask; six services at this scale don't need per-service namespace isolation yet — RBAC/network boundaries come from Workload Identity + NetworkPolicy, not namespace sprawl. Revisit only if a service needs a materially different resource quota or a hard multi-tenant boundary.

## Risks / Trade-offs

- [Bringing AKS/Key Vault/ACR under Terraform management risks accidental resource recreation if attributes drift from the imported state] → Mitigation: `terraform plan` reviewed manually before the first `apply` on these imports specifically; no `terraform apply -auto-approve` for this change's first run (temporarily require manual apply, revert to the existing auto-apply-on-merge afterward).
- [Recreating the AKS cluster (if the current network plugin can't support private link/NetworkPolicy) means re-provisioning workload identities, re-running `helm install` on every service, and re-pointing CI/kubeconfig at a new cluster] → Mitigation: worth doing precisely because nothing is in prod yet — no live traffic to cut over, no downtime window to coordinate; this is strictly a one-time Terraform/CI config change, not an operational migration.
- [Private endpoints + delegated subnets add real Terraform surface (DNS zones, zone links, NSGs) that has to be correct for anything to resolve] → Mitigation: `network.tf` is a self-contained module reviewed independently in `terraform plan` before the workload-facing changes (Postgres, per-service identities) are layered on top; verify DNS resolution from a debug pod before wiring any service to depend on it.
- [Workload Identity federated credentials are per-namespace/per-ServiceAccount — a Helm chart bug that reuses a ServiceAccount across two Deployments would silently over-grant access] → Mitigation: one ServiceAccount per service enforced by Helm template convention; add a CI lint step checking each Deployment references a distinct ServiceAccount.
- [Trivy scan gate could block a merge on a vulnerability with no available fix (base image CVE)] → Mitigation: scan config allow-lists CVEs with no fixed version, reviewed quarterly — not a blanket bypass.
- [Six new/changed workflows all gated by one `deploy` GitHub Environment means one reviewer becomes a bottleneck for every service's deploys] → Mitigation: acceptable at two-person team size (matches `azure-entra-auth-iac`'s existing `terraform-apply` reviewer pattern); revisit if team growth makes this a throughput problem.

## Migration Plan

1. Verify the existing AKS cluster's network plugin (`az aks show`). If Azure CNI with an attachable subnet: proceed to import. If `kubenet`/unusable: provision a fresh AKS cluster in the new VNet instead (no data/traffic to migrate, so this is a config-only decision, not a cutover).
2. Terraform: add `network.tf` (VNet, `snet-aks`, `snet-postgres` delegated, `snet-privatelink`, Private DNS Zones) as its own reviewable plan/apply before anything attaches to it.
3. Terraform: import (or create, per step 1) AKS as a managed resource with OIDC issuer + Workload Identity enabled, inside `snet-aks`; import Key Vault and ACR as managed resources and attach private endpoints in `snet-privatelink`; provision Postgres Flexible Server in `snet-postgres`.
4. Terraform: create per-service Managed Identities + federated credentials + Key Vault RBAC assignments scoped per service.
5. `db-migrations` and `api`: swap `Microsoft.Data.Sqlite` for `Npgsql` directly (no dual-provider switch needed — there is no production data to preserve); rewrite `Scripts/V*.sql` for Postgres dialect.
6. Helm: add `SecretProviderClass` + Workload Identity ServiceAccount annotations per existing service (`api`, `email-delivery`, `ingest`) first, deployed via the new CI deploy job; confirm each pod resolves its Key Vault secret and reaches Postgres over the private endpoint before moving to the next.
7. Helm + CI: add `portal-deployment.yaml`, `crawler-cronjob.yaml`, `summarizer-deployment.yaml` and their CI build+deploy pipelines.
8. Rollback: each step ships behind the existing per-workflow `deploy` Environment reviewer checkpoint — a bad deploy is `helm rollback` to the prior release. Because there's no production data yet, there is no separate data-rollback path to maintain.

## Open Questions

- None blocking — VNet topology, Postgres placement, and Trivy are resolved above. Remaining implementation-time check: confirm the existing AKS cluster's network plugin (step 1 of the migration plan) before writing the Terraform import block, since that determines whether the cluster is imported as-is or recreated.
