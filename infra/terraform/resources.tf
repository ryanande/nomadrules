# These resources already exist and are managed outside this change (Helm/manual
# provisioning); referenced here only as RBAC scope targets, not full resource
# blocks. Converting them to fully Terraform-managed resource definitions is a
# follow-up that needs an operator with real Azure access to reconcile current
# config against a resource block (see infra/terraform/README.md).
data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

data "azurerm_key_vault" "main" {
  name                = var.key_vault_name
  resource_group_name = var.resource_group_name
}

data "azurerm_kubernetes_cluster" "main" {
  name                = var.aks_cluster_name
  resource_group_name = var.resource_group_name
}

data "azurerm_container_registry" "main" {
  name                = var.acr_name
  resource_group_name = var.resource_group_name
}
