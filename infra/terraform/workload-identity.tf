# Per-service Azure Workload Identity: one User-Assigned Managed Identity + federated
# credential per service, each with least-privilege Key Vault RBAC scoped to only
# the secrets that service needs (see aks-workload-security spec). Map keys match
# both the Kubernetes ServiceAccount name (infra/helm/templates/workload-identity.yaml)
# and the Helm values.yaml service names.
locals {
  service_secrets = {
    api            = ["postgres-connection-string", "stripe-webhook-secret"]
    ingest         = ["postgres-connection-string", "servicebus-connection-string"]
    summarizer     = ["postgres-connection-string", "claude-api-key"]
    email-delivery = ["postgres-connection-string", "resend-api-key"]
    db-migrations  = ["postgres-connection-string"]
    crawler        = ["servicebus-connection-string", "storage-connection-string"]
  }

  # Flatten to {secret_name -> unique key} for the placeholder secrets that Terraform
  # doesn't generate itself (postgres-connection-string is created in postgres.tf).
  externally_sourced_secrets = toset([
    "stripe-webhook-secret",
    "servicebus-connection-string",
    "storage-connection-string",
    "claude-api-key",
    "resend-api-key",
  ])
}

resource "azurerm_user_assigned_identity" "service" {
  for_each            = local.service_secrets
  name                = "id-nomadrules-${each.key}"
  location            = data.azurerm_resource_group.main.location
  resource_group_name = data.azurerm_resource_group.main.name
}

resource "azurerm_federated_identity_credential" "service" {
  for_each            = local.service_secrets
  name                = "fic-nomadrules-${each.key}"
  resource_group_name = data.azurerm_resource_group.main.name
  parent_id           = azurerm_user_assigned_identity.service[each.key].id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.main.oidc_issuer_url
  subject             = "system:serviceaccount:${var.app_namespace}:${each.key}"
}

# Placeholder secret shells for credentials Terraform can't generate itself (real
# Stripe/Resend/Claude/Service Bus values come from those providers/portals, not
# Terraform). `ignore_changes` on value means an operator rotating the real secret
# in Key Vault directly is never clobbered by a later `terraform apply`.
resource "azurerm_key_vault_secret" "externally_sourced" {
  for_each     = local.externally_sourced_secrets
  name         = each.key
  key_vault_id = azurerm_key_vault.main.id
  value        = "REPLACE_ME"

  lifecycle {
    ignore_changes = [value]
  }
}

# Per-secret least-privilege: Key Vault RBAC supports scoping a role assignment to
# an individual secret's resource ID, not just the whole vault — used here instead
# of a single vault-wide grant per identity, so (for example) the summarizer's
# identity has no path to the Stripe webhook secret at all.
locals {
  service_secret_grants = {
    for pair in flatten([
      for service, secrets in local.service_secrets : [
        for secret in secrets : {
          key     = "${service}-${secret}"
          service = service
          secret  = secret
        }
      ]
    ]) : pair.key => pair
  }
}

resource "azurerm_role_assignment" "service_secret_access" {
  for_each             = local.service_secret_grants
  scope                = "${azurerm_key_vault.main.id}/secrets/${each.value.secret}"
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.service[each.value.service].principal_id
}

# Feeds infra/helm/values.yaml's workloadIdentity.services.<name>.clientId — an
# operator populates these into a values override file (or `--set`) after apply;
# they are not secrets, just identifiers, safe to read from `terraform output`.
output "workload_identity_client_ids" {
  description = "Map of service name -> Managed Identity client ID, for workloadIdentity.services.<name>.clientId in infra/helm/values.yaml"
  value       = { for k, v in azurerm_user_assigned_identity.service : k => v.client_id }
}
