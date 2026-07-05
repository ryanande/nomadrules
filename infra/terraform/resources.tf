# Resource group itself is not managed here — nothing in this change alters the RG,
# so it stays a data source (scope target only).
data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

# --- AKS, Key Vault, and ACR: brought under full management via `import` blocks ---
# These three previously existed as `data` sources only (see git history / README).
# `azure-entra-auth-iac` left them that way because bringing them under management
# needs an operator with real Azure access to reconcile the resource block's
# attributes against the live resource before the first `terraform plan` is
# trustworthy — that reconciliation is NOT done here (no live Azure access in this
# session). The `*_reconcile` variables below are unset placeholders; an operator
# MUST populate them from `az aks show` / `az keyvault show` / `az acr show` output
# and confirm `terraform plan` reports no unexpected diff before ever running
# `terraform apply` against these three resources. See README "Reconciling AKS/Key
# Vault/ACR before the first apply".

import {
  to = azurerm_kubernetes_cluster.main
  id = "/subscriptions/${var.subscription_id}/resourceGroups/${var.resource_group_name}/providers/Microsoft.ContainerService/managedClusters/${var.aks_cluster_name}"
}

resource "azurerm_kubernetes_cluster" "main" {
  name                = var.aks_cluster_name
  location            = data.azurerm_resource_group.main.location
  resource_group_name = data.azurerm_resource_group.main.name
  dns_prefix          = var.aks_reconcile.dns_prefix
  kubernetes_version  = var.aks_reconcile.kubernetes_version
  sku_tier            = var.aks_reconcile.sku_tier

  # Required for per-service Workload Identity (see aks-workload-security spec).
  # If the existing cluster predates this and its network plugin is `kubenet`
  # (not `azure_cni`), these flags — and private-endpoint reachability — cannot
  # be retrofitted; the cluster must be recreated instead (see design.md).
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  default_node_pool {
    name           = var.aks_reconcile.node_pool_name
    vm_size        = var.aks_reconcile.node_vm_size
    node_count     = var.aks_reconcile.node_count
    vnet_subnet_id = azurerm_subnet.aks.id
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin = var.aks_reconcile.network_plugin # must be "azure" (Azure CNI) — see design.md
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

import {
  to = azurerm_key_vault.main
  id = "/subscriptions/${var.subscription_id}/resourceGroups/${var.resource_group_name}/providers/Microsoft.KeyVault/vaults/${var.key_vault_name}"
}

resource "azurerm_key_vault" "main" {
  name                = var.key_vault_name
  location            = data.azurerm_resource_group.main.location
  resource_group_name = data.azurerm_resource_group.main.name
  tenant_id           = var.aks_reconcile.key_vault_tenant_id
  sku_name            = var.aks_reconcile.key_vault_sku

  enable_rbac_authorization = true # already RBAC-mode per azure-entra-auth-iac

  # No public data-plane path — reachable only via the private endpoint in
  # network.tf. NOTE: if the live vault currently has public access enabled,
  # applying this is what flips it off; confirm no client still depends on the
  # public endpoint before applying (see design.md risk).
  public_network_access_enabled = false

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
  }

  lifecycle {
    prevent_destroy = true
  }
}

import {
  to = azurerm_container_registry.main
  id = "/subscriptions/${var.subscription_id}/resourceGroups/${var.resource_group_name}/providers/Microsoft.ContainerRegistry/registries/${var.acr_name}"
}

resource "azurerm_container_registry" "main" {
  name                = var.acr_name
  location            = data.azurerm_resource_group.main.location
  resource_group_name = data.azurerm_resource_group.main.name

  # Premium is required for private endpoint support — an in-place SKU upgrade
  # (Basic/Standard -> Premium) if the live registry isn't already Premium, not
  # a destructive change.
  sku = "Premium"

  public_network_access_enabled = false

  lifecycle {
    prevent_destroy = true
  }
}
