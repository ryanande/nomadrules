# CI's `helm upgrade` deploy step runs as the same identity as terraform apply
# (one OIDC-federated app registration for both — see README.md and entra.tf's
# provider config for why that's the current, deliberately-simpler choice over a
# second narrower identity).
#
# Accepted trade-off (not just "simplification"): this SP holds subscription
# Contributor + User Access Administrator (see README.md) AND is the credential
# all 7 service-deploy workflows (api.yml, crawler.yml, etc.) authenticate as.
# A compromised deploy workflow therefore yields subscription-level Contributor,
# not just AKS-cluster write, which the original two-identity design avoided.
# Accepted for now given team size (2 people); revisit by splitting back into a
# narrower deploy-only identity if that blast radius stops being acceptable.
#
# AKS's Azure RBAC Kubernetes-authorization layer is separate from
# subscription-level Contributor/Owner, so this identity still needs an
# explicit role assignment scoped to the cluster to run kubectl/helm.
data "azuread_client_config" "current" {}

resource "azurerm_role_assignment" "aks_deploy_writer" {
  scope                = azurerm_kubernetes_cluster.main.id
  role_definition_name = "Azure Kubernetes Service RBAC Writer"
  principal_id         = data.azuread_client_config.current.object_id
}
