# --- Subscriber-facing plane: Entra External ID (CIAM) tenant ---
# CIAM lives in a separate tenant from the workforce directory (see design.md),
# so it gets its own provider alias. The Terraform identity must additionally
# be granted Application Administrator in the CIAM tenant during bootstrap.
provider "azuread" {
  alias     = "ciam"
  tenant_id = var.ciam_tenant_id
}

resource "azuread_application" "subscriber_portal" {
  provider     = azuread.ciam
  display_name = "NomadRules Portal (subscribers)"

  web {
    redirect_uris = var.portal_redirect_uris
  }

  single_page_application {
    redirect_uris = var.portal_redirect_uris
  }
}

# ponytail: SPA uses PKCE (see design.md), so no azuread_application_password /
# client secret is defined for the portal app registration.

# --- Workforce plane: Entra ID app roles for team/admin access ---
locals {
  app_role_ids = {
    Admin    = "00000000-0000-0000-0000-000000000001"
    Operator = "00000000-0000-0000-0000-000000000002"
    ReadOnly = "00000000-0000-0000-0000-000000000003"
  }
}

resource "azuread_application" "admin_access" {
  display_name = "NomadRules Admin Access"

  app_role {
    id                   = local.app_role_ids.Admin
    allowed_member_types = ["User"]
    display_name         = "Admin"
    value                = "Admin"
    description          = "Full write access: Key Vault secrets, AKS admin, ACR push"
    enabled              = true
  }

  app_role {
    id                   = local.app_role_ids.Operator
    allowed_member_types = ["User"]
    display_name         = "Operator"
    value                = "Operator"
    description          = "Read secrets, read AKS resources within a namespace"
    enabled              = true
  }

  app_role {
    id                   = local.app_role_ids.ReadOnly
    allowed_member_types = ["User"]
    display_name         = "ReadOnly"
    value                = "ReadOnly"
    description          = "Resource-group Reader, no secrets"
    enabled              = true
  }
}

resource "azuread_service_principal" "admin_access" {
  client_id = azuread_application.admin_access.client_id
}

resource "azuread_app_role_assignment" "team" {
  for_each            = var.team_role_assignments
  app_role_id         = local.app_role_ids[each.value]
  principal_object_id = each.key
  resource_object_id  = azuread_service_principal.admin_access.object_id
}

output "ciam_client_id" {
  value = azuread_application.subscriber_portal.client_id
}

output "ciam_tenant_id" {
  value = var.ciam_tenant_id
}

output "ciam_oidc_discovery_url" {
  value = "https://${var.ciam_tenant_domain}/v2.0/.well-known/openid-configuration"
}
