# Green-field: nothing pre-existed in this subscription (confirmed empty before
# this change), so the resource group, AKS, Key Vault, and ACR are all created
# here, not imported. Cheapest-viable tier choices for a pre-launch, two-person
# project (see openspec/changes/azure-entra-auth-iac follow-up discussion):
# AKS Free tier control plane, a single Standard_B2s node, ACR Basic (no private
# endpoint support at this tier — network.tf only wires a private endpoint for
# Key Vault). Revisit tiers once there's real traffic/uptime requirements.
data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
}

# Short random suffix keeps Key Vault/ACR names globally unique without forcing
# an operator to hand-pick and availability-check a name before every apply.
resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

resource "azurerm_kubernetes_cluster" "main" {
  name                = var.aks_cluster_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "nomadrules"
  sku_tier            = "Free"

  # Required for per-service Workload Identity (see aks-workload-security spec).
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  default_node_pool {
    name           = "system"
    vm_size        = "Standard_B2s"
    node_count     = 1
    vnet_subnet_id = azurerm_subnet.aks.id
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin = "azure" # required for Workload Identity's pod-to-VNet networking — see design.md
  }

  # AKS addon that installs the Secrets Store CSI Driver + Azure Key Vault provider
  # (see infra/helm/templates/workload-identity.yaml's SecretProviderClass objects) —
  # required for any pod to actually resolve a Key Vault secret via Workload Identity.
  key_vault_secrets_provider {
    secret_rotation_enabled = true
  }

  lifecycle {
    # Node pool identity/size/subnet are immutable-in-place for many combinations;
    # never let a plan silently recreate the cluster. An operator must review and
    # explicitly acknowledge any diff here before applying.
    prevent_destroy = true
  }
}

resource "azurerm_key_vault" "main" {
  name                = "kv-nomadrules-${random_string.suffix.result}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  rbac_authorization_enabled = true # RBAC-mode per azure-entra-auth-iac, not access policies

  # No public data-plane path — reachable only via the private endpoint in network.tf.
  public_network_access_enabled = false

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
  }

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_container_registry" "main" {
  name                = "acrnomadrules${random_string.suffix.result}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  # Basic: cheapest tier. No private-link support at this SKU (Premium is
  # required for that — see design.md's original private-endpoint plan); RBAC
  # (rbac.tf's AcrPush grant) is the access control instead of network isolation.
  sku                           = "Basic"
  public_network_access_enabled = true

  lifecycle {
    prevent_destroy = true
  }
}
