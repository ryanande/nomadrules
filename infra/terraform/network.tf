# From-scratch network foundation (nothing pre-existed — see resources.tf). Three
# subnets, one per concern (see design.md "dedicated VNet" decision): AKS nodes, a
# Postgres Flexible Server delegated subnet (cannot be shared with anything else —
# Azure requires the delegation to be exclusive), and a private-endpoints subnet
# for Key Vault (ACR is Basic SKU — no private-link support at that tier, see
# resources.tf, so it has no private endpoint here).
resource "azurerm_virtual_network" "main" {
  name                = "${var.resource_group_name}-vnet"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  address_space       = [var.vnet_address_space]
}

resource "azurerm_subnet" "aks" {
  name                 = "snet-aks"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [var.aks_subnet_prefix]
}

resource "azurerm_subnet" "postgres" {
  name                 = "snet-postgres"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [var.postgres_subnet_prefix]

  delegation {
    name = "postgres-flexible-server"
    service_delegation {
      name    = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_subnet" "privatelink" {
  name                 = "snet-privatelink"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [var.privatelink_subnet_prefix]
}

# Private DNS zones — one per privately-linked service, linked to the VNet so pods
# resolve each hostname to a private IP instead of the public endpoint. Zone names
# are Azure's fixed well-known values for each service, not arbitrary.
resource "azurerm_private_dns_zone" "key_vault" {
  name                = "privatelink.vaultcore.azure.net"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_dns_zone" "postgres" {
  name                = "privatelink.postgres.database.azure.com"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_dns_zone_virtual_network_link" "key_vault" {
  name                  = "kv-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.key_vault.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

# Postgres Flexible Server's VNet-integrated mode links its own DNS zone directly
# (no azurerm_private_endpoint needed — the delegated subnet IS the private path).
resource "azurerm_private_dns_zone_virtual_network_link" "postgres" {
  name                  = "postgres-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.postgres.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

# --- Key Vault private endpoint (snet-privatelink) ---
# Disabling public access on the vault happens on the resource itself in
# resources.tf; this endpoint is the only path in once that's flipped. ACR has no
# equivalent here — Basic SKU doesn't support private link (see resources.tf).
resource "azurerm_private_endpoint" "key_vault" {
  name                = "pe-key-vault"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.privatelink.id

  private_service_connection {
    name                           = "psc-key-vault"
    private_connection_resource_id = azurerm_key_vault.main.id
    subresource_names              = ["vault"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "key-vault-dns"
    private_dns_zone_ids = [azurerm_private_dns_zone.key_vault.id]
  }
}
