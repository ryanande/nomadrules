terraform {
  # Partial config: storage_account_name, container_name, key, and resource_group_name
  # are supplied at `terraform init -backend-config=...` time (CI workflow + local
  # override file), not hardcoded here, since the backend itself is provisioned by
  # the one-time manual bootstrap (see infra/terraform/README.md) before any apply runs.
  backend "azurerm" {
    use_oidc = true
  }
}
