# NomadRules Terraform

Source of truth for Entra app registrations, Entra app roles, Azure RBAC role assignments, the platform VNet, and the AKS/Key Vault/ACR/Postgres resources those roles and workloads depend on. Helm (`infra/helm/`) still owns K8s workload deployment; this root owns identity/IAM, networking, and cloud resource scaffolding.

## Reconciling AKS/Key Vault/ACR before the first apply

`resources.tf` brings AKS, Key Vault, and ACR under full Terraform management via `import` blocks (previously they were `data` sources only — see git history). This is necessary to declare `oidc_issuer_enabled`/`workload_identity_enabled` on the cluster and `public_network_access_enabled = false` on the vault/registry, none of which a `data` source can express.

**None of the resource blocks in `resources.tf` are apply-ready as committed.** The `var.aks_reconcile` object has no defaults on purpose — an operator with real Azure access must:

1. Run `az aks show -g <rg> -n <cluster>`, `az keyvault show -n <vault>`, `az acr show -n <registry>` and populate `var.aks_reconcile` (dns prefix, k8s version, SKU tier, node pool name/size/count, network plugin, Key Vault tenant ID/SKU) in a not-committed `terraform.tfvars` to match the live resources exactly.
2. **Check `network_plugin` first.** If the live cluster is `kubenet` (not `azure_cni`), it cannot support private endpoints or Workload Identity's pod-to-VNet networking — the cluster must be recreated inside `snet-aks` with Azure CNI rather than imported. See design.md's "Verify the existing AKS cluster's network plugin" decision. Do not proceed with the import path if this is the case.
3. Run `terraform plan` and confirm it reports **no diff** on the three imported resources before ever running `apply` against them — a diff here means `var.aks_reconcile` doesn't yet match live state, and applying it would attempt to change real infrastructure.
4. Only once plan is clean should the VNet, Postgres, private endpoints, and per-service Workload Identity resources be applied (they depend on the imported resources but don't themselves require reconciliation — they're new).

This mirrors the same "requires an operator with real Azure access" constraint documented in the original `azure-entra-auth-iac` change; it isn't new risk, just risk that was previously deferred by leaving these three as `data` sources.

## One-time manual bootstrap (cannot be Terraform'd — chicken-and-egg)

Before the first `terraform init`/`apply`, someone with Application Administrator + Owner must, by hand:

1. Confirm Entra External ID (CIAM) is enabled on the tenant (or create the CIAM tenant).
2. Create the Terraform service principal, grant it Owner on the target resource group and Application Administrator on both the workforce and CIAM tenants.
3. Add a GitHub OIDC federated credential on that service principal, scoped to `var.github_repo` + the `main` branch, so CI never stores a client secret.
4. Provision the remote state backend by hand: an Azure Storage account + container with versioning and locking enabled.
5. Create a `terraform-apply` GitHub Environment (Settings > Environments) with required reviewers, so `terraform-apply.yml`'s auto-apply-on-merge always waits for a human checkpoint before touching real infrastructure.

Backend connection details (`storage_account_name`, `container_name`, `resource_group_name`) are passed via `terraform init -backend-config=...`, not hardcoded in `backend.tf`.

## Break-glass account

Exactly one account — the human who ran the manual bootstrap above, or the bootstrap service principal itself — retains subscription **Owner** outside these Terraform-managed role assignments. This is the recovery path if a bad `terraform apply` locks out every other identity. Do not add this account to `var.team_role_assignments`; its access is intentionally out-of-band.

## Variables

Real values (subscription ID, resource names, team member object IDs, break-glass object ID) belong in a `terraform.tfvars` that is **not committed** — see `variables.tf` for the full list.

## Team access model

Three tiers only — `Admin`, `Operator`, `ReadOnly` — each mapped to Azure built-in roles at resource-group or per-resource scope (see `rbac.tf`). No custom RBAC role definitions, no PIM/just-in-time elevation yet (revisit if the team grows past two people or P2 licensing is available).
