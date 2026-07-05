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

variable "github_repo" {
  description = "GitHub repo in 'owner/name' form, used to scope the OIDC federated credential"
  type        = string
  default     = "ryanande/nomadrules"
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
