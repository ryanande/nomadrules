## Why

Today only 3 of 6 services (`ingest`, `email-service`, `db-migrations`) have CI image pipelines, and none of them deploy anywhere — every workflow ends at `docker push`. There is no Helm template for `portal`, `crawler`, or `summarizer`, no shared datastore beyond SQLite, and no Key Vault wiring for workload secrets (Stripe, Resend, Claude API keys, DB connection string) — `infra/terraform` from `azure-entra-auth-iac` provisions *identity* (Entra/RBAC) but references AKS/Key Vault/ACR only as data sources, never actually deploying a workload into them. Running the full system in AKS — with Key Vault as the only source of runtime secrets and CI actually shipping code — is the last mile between "infrastructure exists" and "the product runs."

## What Changes

- New CI build pipelines for `api`, `crawler`, `summarizer`, and `portal` (the three existing pipelines — `ingest`, `email-delivery`, `db-migrations` — already establish the pattern to copy).
- **BREAKING**: every workflow gains a deploy job — `helm upgrade` against the single AKS cluster on merge to `main`, gated by a required-reviewer GitHub Environment (same checkpoint pattern as `terraform-apply.yml`). CI stops being build-only.
- New Helm templates: `portal-deployment.yaml`, `crawler-cronjob.yaml`, `summarizer-deployment.yaml`, plus a `SecretProviderClass` per workload that needs Key Vault secrets.
- Per-service Azure Workload Identity: one User-Assigned Managed Identity + federated credential per workload (api, crawler, summarizer, email-service, ingest, db-migrations), each with least-privilege Key Vault RBAC scoped only to the secrets that service needs. No plaintext secrets in `values.yaml` or checked-in K8s `Secret` manifests.
- New dedicated VNet (`infra/terraform/network.tf`) — none exists today — with subnets for AKS nodes, a delegated Postgres subnet, and a private-endpoints subnet, plus Private DNS Zones so in-cluster DNS resolves Key Vault, ACR, and Postgres to private IPs.
- AKS cluster gains OIDC issuer + Workload Identity enabled and moves inside the new VNet (currently a Terraform data source only — this change brings the cluster under full `azurerm_kubernetes_cluster` management). If the existing cluster's network plugin can't support private endpoints/NetworkPolicy, it is recreated inside the new VNet rather than retrofitted — cheap to do now since nothing is in production.
- **BREAKING**: Key Vault and ACR public network access is disabled; both get private endpoints in the new VNet.
- **BREAKING**: shared datastore moves from SQLite to Azure Database for PostgreSQL Flexible Server — private-networked (delegated subnet, no public endpoint). `db-migrations` and `api` swap `Microsoft.Data.Sqlite` for `Npgsql` directly; no dual-provider cutover needed since there is no production data to migrate.
- Container image scanning (Trivy) added as a CI gate before push.
- ArgoCD is explicitly **out of scope** for this change — CI-driven `helm upgrade` is the mature-enough deploy mechanism today; GitOps is a fast-follow once there are enough environments/clusters to justify a pull-based reconciler (see design.md Non-Goals).

## Capabilities

### New Capabilities

- `aks-workload-security`: Per-service Azure Workload Identity + Key Vault Secrets Store CSI Driver, least-privilege RBAC scoped per workload — no plaintext secrets anywhere in git or in `kubectl get secret -o yaml`.
- `platform-continuous-deployment`: CI builds and deploys every service to the single AKS cluster via `helm upgrade` on merge to `main`, human-reviewed via GitHub Environment protection, with image vulnerability scanning as a merge gate.
- `postgres-datastore`: Azure Database for PostgreSQL Flexible Server as the shared datastore for all services, network-isolated to the AKS VNet, connection secret sourced from Key Vault via Workload Identity (never an env var literal).

### Modified Capabilities

- `db-migration-runner`: connection string requirement changes from `SQLITE_CONNECTION_STRING`/`ConnectionStrings:Sqlite` to a Postgres connection string resolved via Key Vault-mounted secret (Workload Identity), and the runner's script dialect moves from SQLite to Postgres SQL.

## Impact

- New `infra/terraform/network.tf` — VNet, `snet-aks`, `snet-postgres` (delegated), `snet-privatelink`, Private DNS Zones
- `infra/terraform/resources.tf` — `azurerm_kubernetes_cluster`, `azurerm_key_vault`, `azurerm_container_registry` become fully managed (imported, or recreated if the network plugin requires it) resources instead of `data` blocks, each attached to the new VNet; new `azurerm_postgresql_flexible_server` in the delegated subnet; new `azurerm_private_endpoint` for Key Vault and ACR; new `azurerm_user_assigned_identity` + `azurerm_federated_identity_credential` per workload; new Key Vault RBAC role assignments scoped per identity
- `infra/helm/` — new `portal-deployment.yaml`, `crawler-cronjob.yaml`, `summarizer-deployment.yaml`, `SecretProviderClass` templates; `values.yaml` gains per-service workload identity client IDs and drops any plaintext secret placeholders
- `.github/workflows/` — new `api.yml`, `crawler.yml`, `summarizer.yml`, `portal.yml` (build); every workflow (new and existing) gains a `deploy` job; new `deploy` GitHub Environment with required reviewers
- `src/db-migrations/NomadRules.DbMigrations/` — `Microsoft.Data.Sqlite` → `Npgsql`; `Scripts/V*.sql` rewritten for Postgres dialect
- `src/api/NomadRules.Api/` — connection string resolution swaps to Npgsql; local dev docker-compose gains a Postgres container
- `infra/docker-compose.yml` — Postgres replaces SQLite for local parity with production
- Depends on `azure-entra-auth-iac`'s Terraform foundation (Entra/RBAC) already in `infra/terraform/`; this change extends the same root rather than creating a second one
