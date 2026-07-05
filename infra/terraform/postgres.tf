# Azure Database for PostgreSQL Flexible Server, VNet-integrated (delegated subnet,
# see network.tf) — no public network access. Replaces SQLite as the shared
# datastore (see design.md "Azure Database for PostgreSQL Flexible Server" decision).
# Nothing is in production yet, so this provisions a fresh server with no data
# migration/cutover concerns.
resource "random_password" "postgres_admin" {
  length  = 32
  special = false # keeps the generated password safe to embed in a connection string without escaping
}

resource "azurerm_postgresql_flexible_server" "main" {
  name                = "${var.resource_group_name}-postgres"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = data.azurerm_resource_group.main.location

  delegated_subnet_id = azurerm_subnet.postgres.id
  private_dns_zone_id = azurerm_private_dns_zone.postgres.id

  # Burstable B1ms: cheapest tier that fits this workload (pre-100 subscribers,
  # low write volume) — see design.md cost comparison vs. in-cluster Postgres.
  sku_name   = var.postgres_sku_name
  storage_mb = var.postgres_storage_mb
  version    = "16"

  administrator_login    = "nomadrules_admin"
  administrator_password = random_password.postgres_admin.result

  backup_retention_days = 7

  depends_on = [azurerm_private_dns_zone_virtual_network_link.postgres]
}

resource "azurerm_postgresql_flexible_server_database" "main" {
  name      = "nomadrules"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_key_vault_secret" "postgres_connection_string" {
  name         = "postgres-connection-string"
  key_vault_id = azurerm_key_vault.main.id
  value        = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Database=${azurerm_postgresql_flexible_server_database.main.name};Username=${azurerm_postgresql_flexible_server.main.administrator_login};Password=${random_password.postgres_admin.result};SSL Mode=Require"
}
