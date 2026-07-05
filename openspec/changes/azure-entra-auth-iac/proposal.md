## Why

Subscriber login is a hand-rolled magic-link + HMAC-JWT scheme (`AuthService.cs`), and there is no admin/team access model at all — anyone with `kubectl`/cloud console access has unbounded reach into Key Vault, the database, and compute. `docs/04-tech-stack.md` already earmarked "Azure AD B2C or Auth0" for consumer auth as the intended v1 direction, with magic links as the explicit v0.1 stopgap (CLAUDE.md principle #8). There is also zero Infrastructure-as-Code today (`infra/` has Helm charts only) — every Azure resource, app registration, and role assignment that would back this is click-ops. Doing identity right and doing it as code are the same change: you can't "vet permissions" against IaC that doesn't exist.

## What Changes

- **BREAKING**: Subscriber authentication moves from magic-link + custom JWT to Azure Entra External ID (CIAM). `POST /api/auth/magic-link`, `GET /api/auth/verify`, and the `magic_links` table are removed; the portal's `Login.tsx` sign-in/register flow is replaced with Entra's hosted sign-in/sign-up user flow.
- API validates Entra-issued tokens directly (JWT bearer against Entra's OIDC metadata) instead of minting its own JWT; the `nr_token` cookie handoff is replaced by a token acquired via MSAL in the portal.
- New Entra ID (workforce tenant) app registration for team/admin access, with app roles (e.g. `Admin`, `Operator`, `ReadOnly`) mapped to least-privilege Azure RBAC role assignments scoped per resource (Key Vault, Postgres/SQLite volume, AKS namespace, container registry) — no more standing owner/contributor access.
- New Terraform root under `infra/terraform/` as the source of truth for: Entra app registrations (both tenants), app roles, Azure RBAC role assignments, and the underlying resource references those roles scope to. Existing Azure resources (Key Vault, AKS, ACR, Postgres) get imported or (if not yet provisioned) created here — Helm remains the K8s workload deployment layer, Terraform owns identity/IAM and cloud resource scaffolding.
- CI gets a `terraform plan` check on PR and `terraform apply` on merge to main (mirrors the existing image-build workflow pattern in `.github/workflows/`).

## Capabilities

### New Capabilities

- `subscriber-auth`: Subscriber-facing authentication via Azure Entra External ID (CIAM) — hosted sign-in/sign-up, token issuance, and API-side token validation. Replaces the magic-link capability implicit in today's `AuthService`/`AuthEndpoints`.
- `admin-access-control`: Entra ID (workforce tenant) app roles plus Azure RBAC least-privilege assignments for team/admin access to Azure resources (Key Vault, database, AKS, ACR). No interactive/manual role grants.
- `azure-iac-foundation`: Terraform IaC covering Entra app registrations/roles for both tenants, Azure RBAC role assignments, and the Azure resources those roles scope to (Key Vault, AKS, ACR, Postgres) — plan-on-PR/apply-on-merge via CI.

### Modified Capabilities

<!-- No existing openspec/specs/* capability changes requirements; today's magic-link auth was never spec'd. It is superseded by `subscriber-auth` above rather than deltad. -->

## Impact

- `src/api/NomadRules.Api/Features/Auth/` — `AuthService.cs` and `AuthEndpoints.cs` rewritten for Entra token validation; magic-link issuance code deleted
- `src/api/NomadRules.Api/Program.cs` — JWT bearer config points at Entra External ID OIDC metadata instead of a shared HMAC secret
- `src/db-migrations/.../Scripts/` — new migration dropping `magic_links` table
- `src/portal/src/pages/Login.tsx`, `src/portal/src/lib/api.ts` — replaced with MSAL-based redirect/token flow
- New `infra/terraform/` root (providers: `azurerm`, `azuread`); state backend (Azure Storage) provisioned as a one-time bootstrap
- `infra/helm/values.yaml` — API gains Entra tenant/client ID config (non-secret) via env vars; `Jwt:Secret` config removed
- `.github/workflows/` — new `terraform-plan.yml` / `terraform-apply.yml`
- Azure Key Vault — access policy migrates from any standing grants to RBAC-mode, backed by the new role assignments
- Known dependency: requires an Azure AD tenant with Entra External ID (CIAM) enabled, and Global Admin (or equivalent privileged role) to create the workforce-tenant app registration and grant admin consent — a one-time manual bootstrap step outside Terraform's reach (see design.md)
