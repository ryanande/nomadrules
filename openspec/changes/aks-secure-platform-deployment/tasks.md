## 1. Network foundation

- [ ] 1.1 Confirm the existing AKS cluster's network plugin via `az aks show` (Azure CNI with an attachable subnet vs. `kubenet`/unusable) — determines whether the cluster is imported as-is or recreated. **Blocked: requires live Azure CLI access, not available in this session.**
- [x] 1.2 Add `infra/terraform/network.tf`: VNet, `snet-aks`, `snet-postgres` (delegated to `Microsoft.DBforPostgreSQL/flexibleServers`), `snet-privatelink`
- [x] 1.3 Add Private DNS Zones (Key Vault, ACR, Postgres) linked to the VNet
- [ ] 1.4 Review and apply `network.tf` independently before any workload-facing resource attaches to it. **Blocked: requires a real Azure subscription + backend state; code is authored and `terraform fmt`-clean but unapplied.**

## 2. Terraform — bring AKS/Key Vault/ACR under management, inside the VNet

- [x] 2.1 Author the `import` blocks + `azurerm_kubernetes_cluster`/`azurerm_key_vault`/`azurerm_container_registry` resource definitions (was: `terraform import` the existing resources). **Reconciliation against live values (`var.aks_reconcile`) still requires an operator with real Azure access — see README "Reconciling AKS/Key Vault/ACR before the first apply".**
- [ ] 2.2 Review the resulting `terraform plan` by hand to confirm no unintended recreation before any apply. **Blocked: no live Azure access in this session.**
- [x] 2.3 Add `oidc_issuer_enabled = true` and `workload_identity_enabled = true` to the managed `azurerm_kubernetes_cluster` resource
- [x] 2.4 Add `azurerm_private_endpoint` for Key Vault and ACR in `snet-privatelink`; disable public network access on both
- [ ] 2.5 Apply (manual, non-auto-approve for this step) and verify cluster OIDC issuer URL is populated and Key Vault/ACR resolve to private IPs from inside the VNet. **Blocked: requires a real cluster to apply against.**

## 3. Postgres provisioning

- [x] 3.1 Add `azurerm_postgresql_flexible_server` in `snet-postgres`, no public network access
- [x] 3.2 Store the generated admin connection string as a Key Vault secret
- [ ] 3.3 Confirm the server is unreachable from outside the VNet (network-layer test). **Blocked: requires a provisioned server to test against.**

## 4. Per-service Workload Identity

- [x] 4.1 Add `azurerm_user_assigned_identity` + `azurerm_federated_identity_credential` per service (api, crawler, summarizer, email-delivery, ingest, db-migrations)
- [x] 4.2 Add Key Vault RBAC role assignments per identity, scoped to only the secrets that service needs (per-secret scope, not vault-wide)
- [x] 4.3 Enable the Secrets Store CSI Driver + Azure Key Vault provider addon on the AKS cluster (`key_vault_secrets_provider` block in `resources.tf` — this was missing initially and has been added)
- [x] 4.4 Add a `SecretProviderClass` Helm template parameterized per service (`workload-identity.yaml`, ranges over `values.workloadIdentity.services`)

## 5. Postgres cutover

- [x] 5.1 Swap `Microsoft.Data.Sqlite` for `Npgsql` — **scope note:** this originally named only `db-migrations` and `api`, but `ingest`, `summarizer`, and `email-service` each have their own hand-rolled `Db.cs`/SQLite connection too (discovered during apply); all five were swapped, not just two.
- [x] 5.2 Rewrite `Scripts/V*.sql` for Postgres dialect (`datetime('now')` → `now()::text`); added `V005__summarizer_email_columns.sql` folding in the summarizer's and email-service's ad-hoc self-migration `Migrations.cs` files (both deleted — their own comments already flagged them as a stopgap "until DbUp migrations land")
- [x] 5.3 Update connection string resolution to `POSTGRES_CONNECTION_STRING` / `ConnectionStrings:Postgres` across all five services
- [x] 5.4 Swap the same provider in `src/api/NomadRules.Api/`, plus fix SQLite-specific SQL (`INSERT OR IGNORE` → `ON CONFLICT ... DO NOTHING`, `SqliteException` → `PostgresException`/`23505`) in `AuthService.cs`, `SubscriberService.cs`, `DeliveryRepository.cs`
- [x] 5.5 Add Postgres to `infra/docker-compose.yml` for local dev parity (no root compose existed before this change — added one)

**Verified, not just authored:** ran the actual migration runner against a real local Postgres container — all 6 scripts applied cleanly, re-run confirmed idempotent (0 scripts on second pass), and the exact rewritten queries (`now()::text`, both `ON CONFLICT` dedup paths, the pre-existing `ON CONFLICT(source_message_id)` in ingest) were exercised directly against the resulting schema and behaved correctly. All five modified C# projects build with 0 errors/warnings; ingest/summarizer/email-service `--selfcheck` still pass.

## 6. Helm — service coverage and secret wiring

- [x] 6.1 Add ServiceAccount + Workload Identity annotations and `SecretProviderClass` reference to existing `api-deployment.yaml`, `email-delivery-cronjob.yaml`, `ingest-deployment.yaml`; removed the old shared `nomadrules-secrets`/SQLite-PVC pattern and the api's per-pod migration init container (migrations now run via the `db-migrations-job.yaml` pre-upgrade hook instead)
- [x] 6.2 Remove any plaintext secret placeholders from `values.yaml`
- [x] 6.3 Add `portal-deployment.yaml` (+ a ClusterIP Service — needed for the Deployment to be reachable at all; Ingress/TLS is out of scope)
- [x] 6.4 Add `crawler-cronjob.yaml`
- [x] 6.5 Add `summarizer-deployment.yaml`
- [x] 6.6 Add a `db-migrations` Helm Job template (`pre-install,pre-upgrade` hook) that runs ahead of every other workload's rollout

**Also added (not in the original task list, but required for any of this to work):** `infra/helm/Chart.yaml` — did not exist at all before this change, so `helm upgrade --install` (used by every CI deploy job) would have failed immediately regardless of anything else here. Verified with `helm template` + `helm lint` — renders 20 resources cleanly (4 Deployments, 2 CronJobs, 1 Job, 6 SecretProviderClass, 6 ServiceAccount, 1 Service), 0 lint errors.

## 7. CI — build pipelines for uncovered services

- [x] 7.1 Add `api.yml` image-build workflow
- [x] 7.2 Add `crawler.yml` image-build workflow (also added `src/crawler/Dockerfile` — none existed; verified `npm run build` succeeds)
- [x] 7.3 Add `summarizer.yml` image-build workflow
- [x] 7.4 Add `portal.yml` image-build workflow (also added `src/portal/Dockerfile` + `nginx.conf` — none existed; verified `npm run build` succeeds)
- [x] 7.5 Add a Trivy scan step to every image-build workflow (all seven); added `.trivyignore`

## 8. CI — continuous deployment

- [ ] 8.1 Create the `deploy` GitHub Environment with required reviewers. **Blocked: repo-admin action (GitHub Settings > Environments) — not attempted without explicit confirmation, same as the existing `terraform-apply` Environment in `azure-entra-auth-iac`.**
- [x] 8.2 Add a deploy job (OIDC Azure login via a new, purpose-built `AZURE_DEPLOY_CLIENT_ID` identity — **not** the Terraform-apply identity, see `infra/terraform/ci-deploy.tf` — `az aks get-credentials`, `helm upgrade --reuse-values --set <service>.image.tag=<sha>`) to each of the seven service workflows, gated on the `deploy` environment
- [ ] 8.3 Roll out the deploy job to `api`/`email-delivery`/`ingest` first, confirm each pod resolves its Key Vault secret and reaches Postgres over the private endpoint before proceeding. **Blocked: requires a real cluster.**
- [ ] 8.4 Roll out to `portal`/`crawler`/`summarizer`/`db-migrations`. **Blocked: same as above.**

## 9. Cleanup

- [x] 9.1 Remove `Microsoft.Data.Sqlite` package references (all five projects) and SQLite scripts; also deleted two now-dead self-migration files (`Migrations.cs` in summarizer and email-service) and stale local `.db` files that predated this change
- [x] 9.2 Update `CLAUDE.md`'s Database and Deployment rows, and principle #8, to reflect Postgres + CI-driven `helm upgrade` as deployed (not planned). `docs/04-tech-stack.md` was found to be broadly aspirational/pre-pivot (Cosmos DB, NServiceBus, Azure Functions — none matching reality) — added a note pointing to `CLAUDE.md` as authoritative rather than attempting a full unrelated rewrite of that document.

## What's left before this can run in production

Everything requiring live Azure/GitHub-admin access is unchecked above by design — this session has no cloud credentials. In order, an operator needs to:
1. Populate `var.aks_reconcile` from real `az aks show`/`az keyvault show` output (see README) and confirm the network plugin (task 1.1) before the AKS import can be trusted.
2. Run `terraform plan`/`apply` for `network.tf` first, then the rest, in the order the migration plan in `design.md` lays out.
3. Create the `AZURE_DEPLOY_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID`/`AKS_RESOURCE_GROUP`/`AKS_CLUSTER_NAME` GitHub Actions secrets (the first from `terraform output ci_deploy_client_id`) and the `deploy` GitHub Environment with required reviewers.
4. Merge to `main` — the seven build+deploy workflows take it from there.
