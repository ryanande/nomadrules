variable "subscription_id" {
  description = "Azure subscription ID hosting NomadRules resources"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group containing the Key Vault, AKS cluster, ACR, and DB"
  type        = string
}

variable "key_vault_name" {
  description = "Name of the existing Key Vault to bring under RBAC management"
  type        = string
}

variable "aks_cluster_name" {
  description = "Name of the existing AKS cluster"
  type        = string
}

variable "acr_name" {
  description = "Name of the existing Azure Container Registry"
  type        = string
}

variable "ciam_tenant_id" {
  description = "Tenant ID (GUID) of the separate Entra External ID (CIAM) tenant — distinct from the workforce tenant the default provider targets"
  type        = string
}

variable "ciam_tenant_domain" {
  description = "Domain of the Entra External ID (CIAM) tenant, e.g. nomadrules.onmicrosoft.com"
  type        = string
}

variable "portal_redirect_uris" {
  description = "Redirect URIs the Portal uses after Entra External ID sign-in (dev + prod)"
  type        = list(string)
  default     = ["http://localhost:5173", "https://portal.nomadrules.com"]
}

variable "team_role_assignments" {
  description = "Map of workforce Entra ID object ID -> app role name (Admin, Operator, or ReadOnly) for each team member"
  type        = map(string)
  # ponytail: no default — real object IDs are supplied via terraform.tfvars (not committed)

  validation {
    condition     = alltrue([for role in values(var.team_role_assignments) : contains(["Admin", "Operator", "ReadOnly"], role)])
    error_message = "Each team_role_assignments value must be one of: Admin, Operator, ReadOnly."
  }
}

variable "break_glass_object_id" {
  description = "Object ID of the single break-glass account retaining subscription Owner outside these role assignments (see infra/terraform/README.md)"
  type        = string
}

# --- Network foundation (network.tf) ---
variable "vnet_address_space" {
  description = "Address space for the new platform VNet"
  type        = string
  default     = "10.20.0.0/16"
}

variable "aks_subnet_prefix" {
  description = "Address prefix for the AKS node subnet"
  type        = string
  default     = "10.20.0.0/20"
}

variable "postgres_subnet_prefix" {
  description = "Address prefix for the Postgres Flexible Server delegated subnet"
  type        = string
  default     = "10.20.16.0/24"
}

variable "privatelink_subnet_prefix" {
  description = "Address prefix for the Key Vault / ACR private-endpoints subnet"
  type        = string
  default     = "10.20.17.0/24"
}

variable "app_namespace" {
  description = "Kubernetes namespace the app workloads (and their ServiceAccounts) run in"
  type        = string
  default     = "nomadrules-services"
}

# --- AKS/Key Vault reconciliation (resources.tf) ---
# No defaults on purpose: these MUST be populated from `az aks show` / `az keyvault
# show` output before the first `terraform plan` against the imported resources is
# trustworthy. See README "Reconciling AKS/Key Vault/ACR before the first apply".
variable "aks_reconcile" {
  description = "Live AKS/Key Vault attributes an operator must confirm before importing (see README.md) — no defaults, must be supplied via terraform.tfvars"
  type = object({
    dns_prefix          = string
    kubernetes_version  = string
    sku_tier            = string
    node_pool_name      = string
    node_vm_size        = string
    node_count          = number
    network_plugin      = string # must be "azure" — see design.md; "kubenet" means the cluster needs recreating, not importing
    key_vault_tenant_id = string
    key_vault_sku       = string
  })
}

# --- Postgres (postgres.tf) ---
variable "postgres_sku_name" {
  description = "Postgres Flexible Server compute tier (Burstable B1ms is the default fit for current data volume — see design.md)"
  type        = string
  default     = "B_Standard_B1ms"
}

variable "postgres_storage_mb" {
  description = "Postgres Flexible Server storage size in MB"
  type        = number
  default     = 32768
}
