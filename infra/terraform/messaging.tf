# Crawler -> ingest message queue and the crawler's snapshot store. Their connection
# strings land in Key Vault under the names the Helm SecretProviderClasses already read.
#
# Basic SKU: the code only uses a plain queue (peek-lock, explicit complete and
# dead-letter), which Basic supports. Basic/Standard have a public endpoint with
# SAS auth only; IP rules and private endpoints need Premium (~$670/month).

resource "azurerm_servicebus_namespace" "main" {
  name                = var.service_bus_namespace_name
  location            = data.azurerm_resource_group.main.location
  resource_group_name = data.azurerm_resource_group.main.name
  sku                 = var.service_bus_sku
  minimum_tls_version = "1.2"
}

resource "azurerm_servicebus_queue" "law_changes" {
  name         = var.service_bus_queue_name
  namespace_id = azurerm_servicebus_namespace.main.id
}

# Queue-scoped SAS rule (send + listen) rather than the namespace root key.
resource "azurerm_servicebus_queue_authorization_rule" "app" {
  name     = "app"
  queue_id = azurerm_servicebus_queue.law_changes.id
  listen   = true
  send     = true
  manage   = false
}

resource "azurerm_storage_account" "crawler" {
  name                            = var.storage_account_name
  location                        = data.azurerm_resource_group.main.location
  resource_group_name             = data.azurerm_resource_group.main.name
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  account_kind                    = "StorageV2"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
}

resource "azurerm_key_vault_secret" "servicebus_connection_string" {
  name         = "servicebus-connection-string"
  key_vault_id = azurerm_key_vault.main.id
  value        = azurerm_servicebus_queue_authorization_rule.app.primary_connection_string

  depends_on = [time_sleep.key_vault_rbac_propagation]
}

resource "azurerm_key_vault_secret" "storage_connection_string" {
  name         = "storage-connection-string"
  key_vault_id = azurerm_key_vault.main.id
  value        = azurerm_storage_account.crawler.primary_connection_string

  depends_on = [time_sleep.key_vault_rbac_propagation]
}

# These two were placeholder secrets in externally_sourced; adopt them in place so the
# existing vault entries are updated rather than deleted (purge protection would block
# re-creating a soft-deleted secret of the same name).
moved {
  from = azurerm_key_vault_secret.externally_sourced["servicebus-connection-string"]
  to   = azurerm_key_vault_secret.servicebus_connection_string
}

moved {
  from = azurerm_key_vault_secret.externally_sourced["storage-connection-string"]
  to   = azurerm_key_vault_secret.storage_connection_string
}
