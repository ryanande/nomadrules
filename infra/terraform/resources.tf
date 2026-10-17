# Resource group itself is not managed here — nothing in this change alters the RG,
# so it stays a data source (scope target only).
data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

# --- AKS, Key Vault, and ACR: created and managed here ---
# The resource group (and the tfstate backend) are bootstrapped by hand; everything
# below is created by `terraform apply`.

data "azurerm_client_config" "current" {}

resource "azurerm_kubernetes_cluster" "main" {
  name                = var.aks_cluster_name
  location            = data.azurerm_resource_group.main.location
  resource_group_name = data.azurerm_resource_group.main.name
  dns_prefix          = var.aks_cluster_name
  sku_tier            = "Free"

  # Required for per-service Workload Identity (see aks-workload-security spec).
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  default_node_pool {
    name           = "system"
    vm_size        = var.aks_node_vm_size
    node_count     = var.aks_node_count
    vnet_subnet_id = azurerm_subnet.aks.id
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin = "azure" # Azure CNI — required for private endpoints / Workload Identity networking
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
  name                = var.key_vault_name
  location            = data.azurerm_resource_group.main.location
  resource_group_name = data.azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  rbac_authorization_enabled = true # already RBAC-mode per azure-entra-auth-iac

  # AKS pods reach the vault through the private endpoint in network.tf. The CI runner has
  # no VNet path, so the public endpoint stays on but is closed to everything except the
  # runner's own IP (var.key_vault_allowed_ips, set per run by the workflows).
  public_network_access_enabled = true
  purge_protection_enabled      = true
  soft_delete_retention_days    = 90

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
    ip_rules       = var.key_vault_allowed_ips
  }

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_container_registry" "main" {
  name                = var.acr_name
  location            = data.azurerm_resource_group.main.location
  resource_group_name = data.azurerm_resource_group.main.name

  # Premium is required for private endpoint support.
  sku = "Premium"

  public_network_access_enabled = false

  lifecycle {
    prevent_destroy = true
  }
}

# The vault is RBAC-mode, so writing secrets needs a data-plane role (Contributor and
# User Access Administrator are control-plane only). RBAC takes a while to propagate.
resource "azurerm_role_assignment" "terraform_key_vault_secrets_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "time_sleep" "key_vault_rbac_propagation" {
  create_duration = "60s"
  depends_on      = [azurerm_role_assignment.terraform_key_vault_secrets_officer]
}
