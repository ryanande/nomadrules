# Azure RBAC is separate from the Entra app roles in entra.tf (app roles gate
# access to NomadRules' own tooling; RBAC gates access to ARM resources) but both
# are driven by the same var.team_role_assignments map so a team member's tier
# grants both consistently.
locals {
  # Each tier's built-in Azure roles + the scope they apply at, per design.md.
  role_grants = {
    Admin = [
      { azure_role = "Key Vault Secrets Officer", scope = data.azurerm_resource_group.main.id },
      { azure_role = "Azure Kubernetes Service RBAC Admin", scope = data.azurerm_kubernetes_cluster.main.id },
      { azure_role = "AcrPush", scope = data.azurerm_container_registry.main.id },
    ]
    Operator = [
      { azure_role = "Key Vault Secrets User", scope = data.azurerm_resource_group.main.id },
      { azure_role = "Azure Kubernetes Service RBAC Reader", scope = data.azurerm_kubernetes_cluster.main.id },
    ]
    ReadOnly = [
      { azure_role = "Reader", scope = data.azurerm_resource_group.main.id },
    ]
  }

  # Flatten {object_id -> tier} x {tier -> grants} into one map keyed for for_each.
  team_grants = {
    for pair in flatten([
      for object_id, tier in var.team_role_assignments : [
        for grant in local.role_grants[tier] : {
          key        = "${object_id}-${grant.azure_role}"
          object_id  = object_id
          azure_role = grant.azure_role
          scope      = grant.scope
        }
      ]
    ]) : pair.key => pair
  }
}

resource "azurerm_role_assignment" "team" {
  for_each             = local.team_grants
  principal_id         = each.value.object_id
  role_definition_name = each.value.azure_role
  scope                = each.value.scope
}
