# NomadRules Terraform

Source of truth for Entra app registrations, Entra app roles, Azure RBAC role assignments, the platform VNet, and the AKS/Key Vault/ACR/Postgres resources those roles and workloads depend on. Helm (`infra/helm/`) still owns K8s workload deployment; this root owns identity/IAM, networking, and cloud resource scaffolding.

## What Terraform creates

AKS, Key Vault, ACR, the VNet, Postgres, private endpoints, workload identities, RBAC, and the Entra app registrations are all created by `terraform apply`. The only things made by hand are listed under "One-time manual bootstrap" below: the resource group (`data` source here), the state backend, and the CI service principal with its roles.

Inputs reach CI as GitHub repo Variables/Secrets mapped to `TF_VAR_*` in `terraform-plan.yml` / `terraform-apply.yml` (`AZURE_RESOURCE_GROUP`, `KEY_VAULT_NAME`, `AKS_CLUSTER_NAME`, `ACR_NAME`, `TF_VAR_CIAM_TENANT_ID`, `CIAM_TENANT_DOMAIN`, `TEAM_ROLE_ASSIGNMENTS`, `BREAK_GLASS_OBJECT_ID`, secret `AZURE_SUBSCRIPTION_ID`). Key Vault has purge protection on and a firewall that admits only the CI runner's IP (injected per run as `TF_VAR_key_vault_allowed_ips`) plus the AKS private endpoint; Terraform grants itself `Key Vault Secrets Officer` to write secret shells. Node pool size defaults to `Standard_D2s_v6` x2 (this subscription has no DSv5/B-series quota in eastus2) and Postgres to `GP_Standard_D2ads_v5` (Burstable is unavailable here) because the subscription has no DSv5 quota.

## One-time manual bootstrap (cannot be Terraform'd — chicken-and-egg)

Before the first `terraform init`/`apply`, someone with Application Administrator + Owner must, by hand:

1. Confirm Entra External ID (CIAM) is enabled on the tenant (or create the CIAM tenant).
2. Create the resource group (`rg-nomadrules`) and the Terraform service principal (`Terraform-NomadRules`), grant it `Contributor` + `User Access Administrator` on the target subscription and `Application Administrator` on both the workforce and CIAM tenants.
3. Add two GitHub OIDC federated credentials on that service principal — one for `push`-to-`main` (`repo:<owner>/<repo>:ref:refs/heads/main`, used by `terraform-apply.yml`) and one for `pull_request` (`repo:<owner>/<repo>:pull_request`, used by `terraform-plan.yml`) — so CI never stores a client secret. This is the **same identity** the deploy workflows (`api.yml`, `crawler.yml`, etc.) use, via the `AZURE_CLIENT_ID` secret — see `ci-deploy.tf`.
4. Provision the remote state backend by hand: an Azure Storage account + container with versioning enabled; grant the service principal `Storage Blob Data Contributor` on it (the backend uses Entra auth). Set the resulting names as GitHub repo **Variables** (Settings > Variables, not Secrets — they aren't sensitive): `TF_BACKEND_RESOURCE_GROUP`, `TF_BACKEND_STORAGE_ACCOUNT`.
5. Create a `terraform-apply` GitHub Environment (Settings > Environments) with required reviewers, so `terraform-apply.yml`'s auto-apply-on-merge always waits for a human checkpoint before touching real infrastructure.

Backend connection details (`storage_account_name`, `container_name`, `resource_group_name`) are passed via `terraform init -backend-config=...`. In CI, `terraform-plan.yml`/`terraform-apply.yml` write `backend.ci.hcl` from the `TF_BACKEND_*` repo Variables above before running `terraform init` — the file itself is gitignored (see `backend.ci.hcl.example` for the format) since it's regenerated every run, not committed. Locally, copy `backend.ci.hcl.example` to `backend.ci.hcl` and fill in the real values yourself.

### Accepted trade-off: one identity, not two

The original design (see `openspec/changes/azure-entra-auth-iac/design.md`) called for a separate, narrower identity for CI deploys (`api.yml`, `crawler.yml`, etc. — AKS-writer only) versus the Terraform-apply identity (subscription Contributor + User Access Administrator). What was actually bootstrapped is **one** identity (`Terraform-NomadRules`) used everywhere, via the single `AZURE_CLIENT_ID` secret. This means a compromised deploy workflow now has a path to subscription-level Contributor/User Access Administrator, not just AKS-cluster write. Accepted deliberately given the current two-person team; revisit (split back into two identities) if that blast radius stops being acceptable — see `ci-deploy.tf`.

## Break-glass account

Exactly one account — the human who ran the manual bootstrap above, or the bootstrap service principal itself — retains subscription **Owner** outside these Terraform-managed role assignments. This is the recovery path if a bad `terraform apply` locks out every other identity. Do not add this account to `var.team_role_assignments`; its access is intentionally out-of-band.

## Variables

Real values (subscription ID, resource names, team member object IDs, break-glass object ID) belong in a `terraform.tfvars` that is **not committed** — see `variables.tf` for the full list.

## Team access model

Three tiers only — `Admin`, `Operator`, `ReadOnly` — each mapped to Azure built-in roles at resource-group or per-resource scope (see `rbac.tf`). No custom RBAC role definitions, no PIM/just-in-time elevation yet (revisit if the team grows past two people or P2 licensing is available).

## Messaging and storage (`messaging.tf`)

Terraform provisions the Service Bus namespace (Basic SKU) with the `law-changes` queue and the crawler's storage account, and writes their connection strings to Key Vault as `servicebus-connection-string` and `storage-connection-string` (adopted from the former `REPLACE_ME` placeholders via `moved` blocks). Only `stripe-webhook-secret`, `claude-api-key` and `resend-api-key` remain placeholders to fill by hand. Basic/Standard Service Bus and storage keep a public endpoint (private endpoints need Premium, about $670/month); access is by SAS key / account key held only in Key Vault. After the first apply, restart the ingest deployment (`kubectl -n nomadrules-services rollout restart deploy/nomadrules-ingest`) so it re-reads the secret.
