# CI's `helm upgrade` deploy step runs as the same identity as terraform apply
# (one OIDC-federated app registration for both — see README.md and entra.tf's
# provider config for why that's the current, deliberately-simpler choice over a
# second narrower identity). AKS's Azure RBAC Kubernetes-authorization layer is
# separate from subscription-level Contributor/Owner, so that identity still
# needs an explicit role assignment scoped to the cluster to run kubectl/helm.
data "azuread_client_config" "current" {}

resource "azurerm_role_assignment" "ci_deploy_aks" {
  scope                = azurerm_kubernetes_cluster.main.id
  role_definition_name = "Azure Kubernetes Service RBAC Writer"
  principal_id         = data.azuread_client_config.current.object_id
}
