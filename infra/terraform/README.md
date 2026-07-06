# NomadRules Terraform

Source of truth for Entra app registrations, Entra app roles, Azure RBAC role assignments, the platform VNet, and the AKS/Key Vault/ACR/Postgres resources those roles and workloads depend on. Helm (`infra/helm/`) still owns K8s workload deployment; this root owns identity/IAM, networking, and cloud resource scaffolding.

## Green-field: resources.tf creates, it doesn't import

The subscription was confirmed empty before this config was written — no pre-existing AKS/Key Vault/ACR to reconcile against. `resources.tf` creates the resource group, AKS cluster, Key Vault, and ACR directly, at the cheapest viable tier for a pre-launch, two-person project:

- AKS: Free tier control plane (no SLA), single `Standard_B2s` node, Azure CNI (required for Workload Identity's pod-to-VNet networking).
- Key Vault: Standard SKU, public access disabled, reachable only via the private endpoint in `network.tf`.
- ACR: Basic SKU — no private-link support at this tier (Premium is required for that), so it stays publicly reachable with RBAC (`rbac.tf`'s `AcrPush` grant) as the access control instead of network isolation. Revisit if that's not acceptable later.
- Key Vault and ACR names get a random 6-character suffix (`random_string.suffix`) so they don't need to be hand-picked and availability-checked before every apply — both must be globally unique across Azure.

Revisit tiers (AKS Standard control plane, more/bigger nodes, ACR Premium + private endpoint) once there's real traffic or uptime requirements — see `resources.tf`'s header comment.

## One-time manual bootstrap (cannot be Terraform'd — chicken-and-egg)

Before the first `terraform init`/`apply`, someone with Application Administrator + Owner must, by hand:

1. Create the Entra External ID (CIAM) tenant (Entra admin center > Manage tenants > Create > External — see [Microsoft's quickstart](https://learn.microsoft.com/en-us/entra/external-id/customers/quickstart-tenant-setup); tenant name/domain/location are locked in permanently once created, so decide deliberately). Note its tenant ID and domain for `var.ciam_tenant_id`/`var.ciam_tenant_domain`.
2. Create the workforce Terraform service principal (`Terraform-NomadRules`), grant it `Contributor` + `User Access Administrator` on the target subscription and `Application Administrator` in the workforce tenant.
3. Create a **second, dedicated** app registration (`Terraform-NomadRules-CIAM`) directly inside the CIAM tenant, with its own service principal and `Application Administrator` role **within that tenant only**. The workforce identity is single-tenant (`signInAudience: AzureADMyOrg`) and structurally cannot authenticate into the CIAM tenant; converting it to multi-tenant instead was rejected because it would let a compromised workforce/deploy identity reach into the customer-facing tenant too (see `entra.tf`). Its client ID becomes `var.ciam_terraform_client_id` / the `AZURE_CIAM_CLIENT_ID` GitHub secret.
4. Add two GitHub OIDC federated credentials on **each** of the two service principals above — one for `push`-to-`main` (`repo:<owner>/<repo>:ref:refs/heads/main`, used by `terraform-apply.yml`) and one for `pull_request` (`repo:<owner>/<repo>:pull_request`, used by `terraform-plan.yml`) — so CI never stores a client secret. The workforce identity is the **same identity** the deploy workflows (`api.yml`, `crawler.yml`, etc.) use, via the `AZURE_CLIENT_ID` secret — see `ci-deploy.tf`; the CIAM identity is only used by Terraform for the `azuread.ciam` provider alias.
5. Provision the remote state backend by hand: an Azure Storage account + container with versioning and locking enabled. Set the resulting names as GitHub repo **Variables** (Settings > Variables, not Secrets — they aren't sensitive): `TF_BACKEND_RESOURCE_GROUP`, `TF_BACKEND_STORAGE_ACCOUNT`.
6. Create a `terraform-apply` GitHub Environment (Settings > Environments) with required reviewers, so `terraform-apply.yml`'s auto-apply-on-merge always waits for a human checkpoint before touching real infrastructure.

Backend connection details (`storage_account_name`, `container_name`, `resource_group_name`) are passed via `terraform init -backend-config=...`. In CI, `terraform-plan.yml`/`terraform-apply.yml` write `backend.ci.hcl` from the `TF_BACKEND_*` repo Variables above before running `terraform init` — the file itself is gitignored (see `backend.ci.hcl.example` for the format) since it's regenerated every run, not committed. Locally, copy `backend.ci.hcl.example` to `backend.ci.hcl` and fill in the real values yourself.

### Accepted trade-off: one identity, not two

The original design (see `openspec/changes/azure-entra-auth-iac/design.md`) called for a separate, narrower identity for CI deploys (`api.yml`, `crawler.yml`, etc. — AKS-writer only) versus the Terraform-apply identity (subscription Contributor + User Access Administrator). What was actually bootstrapped is **one** identity (`Terraform-NomadRules`) used everywhere, via the single `AZURE_CLIENT_ID` secret. This means a compromised deploy workflow now has a path to subscription-level Contributor/User Access Administrator, not just AKS-cluster write. Accepted deliberately given the current two-person team; revisit (split back into two identities) if that blast radius stops being acceptable — see `ci-deploy.tf`.

## Break-glass account

Exactly one account — the human who ran the manual bootstrap above, or the bootstrap service principal itself — retains subscription **Owner** outside these Terraform-managed role assignments. This is the recovery path if a bad `terraform apply` locks out every other identity. Do not add this account to `var.team_role_assignments`; its access is intentionally out-of-band.

## Variables

Real values (subscription ID, CIAM tenant ID/domain/client ID, team member object IDs, break-glass object ID) belong in a `terraform.tfvars` that is **not committed** — see `variables.tf` for the full list. `resource_group_name`, `aks_cluster_name`, and `location` have sensible defaults and don't need to be set unless you want different ones.

## Team access model

Three tiers only — `Admin`, `Operator`, `ReadOnly` — each mapped to Azure built-in roles at resource-group or per-resource scope (see `rbac.tf`). No custom RBAC role definitions, no PIM/just-in-time elevation yet (revisit if the team grows past two people or P2 licensing is available).
