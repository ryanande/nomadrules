# NomadRules Terraform

Source of truth for Entra app registrations, Entra app roles, and Azure RBAC role assignments. Helm (`infra/helm/`) still owns K8s workload deployment; this root owns identity/IAM plus references to the underlying Azure resources those roles scope to.

## One-time manual bootstrap (cannot be Terraform'd — chicken-and-egg)

Before the first `terraform init`/`apply`, someone with Application Administrator + Owner must, by hand:

1. Confirm Entra External ID (CIAM) is enabled on the tenant (or create the CIAM tenant).
2. Create the Terraform service principal, grant it Owner on the target resource group and Application Administrator on both the workforce and CIAM tenants.
3. Add a GitHub OIDC federated credential on that service principal, scoped to `var.github_repo` + the `main` branch, so CI never stores a client secret.
4. Provision the remote state backend by hand: an Azure Storage account + container with versioning and locking enabled.

Backend connection details (`storage_account_name`, `container_name`, `resource_group_name`) are passed via `terraform init -backend-config=...`, not hardcoded in `backend.tf`.

## Break-glass account

Exactly one account — the human who ran the manual bootstrap above, or the bootstrap service principal itself — retains subscription **Owner** outside these Terraform-managed role assignments. This is the recovery path if a bad `terraform apply` locks out every other identity. Do not add this account to `var.team_role_assignments`; its access is intentionally out-of-band.

## Variables

Real values (subscription ID, resource names, team member object IDs, break-glass object ID) belong in a `terraform.tfvars` that is **not committed** — see `variables.tf` for the full list.

## Team access model

Three tiers only — `Admin`, `Operator`, `ReadOnly` — each mapped to Azure built-in roles at resource-group or per-resource scope (see `rbac.tf`). No custom RBAC role definitions, no PIM/just-in-time elevation yet (revisit if the team grows past two people or P2 licensing is available).
