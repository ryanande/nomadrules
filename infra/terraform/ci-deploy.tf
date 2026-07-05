# Separate identity for CI's `helm upgrade` deploy step (see platform-continuous-deployment
# spec) — deliberately NOT the same service principal terraform-apply.yml uses. That
# identity has Owner on the resource group (needed to provision anything); a deploy
# step only needs to write Kubernetes objects in one AKS cluster, so it gets its own
# federated credential and a narrower built-in role.
resource "azuread_application" "ci_deploy" {
  display_name = "NomadRules CI Deploy"
}

resource "azuread_service_principal" "ci_deploy" {
  client_id = azuread_application.ci_deploy.client_id
}

resource "azuread_application_federated_identity_credential" "ci_deploy_github" {
  application_id = azuread_application.ci_deploy.id
  display_name   = "github-actions-deploy-main"
  audiences      = ["api://AzureADTokenExchange"]
  issuer         = "https://token.actions.githubusercontent.com"
  subject        = "repo:${var.github_repo}:ref:refs/heads/main"
}

resource "azurerm_role_assignment" "ci_deploy_aks" {
  scope                = azurerm_kubernetes_cluster.main.id
  role_definition_name = "Azure Kubernetes Service RBAC Writer"
  principal_id         = azuread_service_principal.ci_deploy.object_id
}

output "ci_deploy_client_id" {
  description = "Set as the AZURE_DEPLOY_CLIENT_ID GitHub Actions secret"
  value       = azuread_application.ci_deploy.client_id
}
