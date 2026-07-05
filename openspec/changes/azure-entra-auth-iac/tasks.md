## 1. Manual bootstrap (outside Terraform, one-time)

> Requires real Azure tenant + GitHub org admin access — cannot be done from this session. Unchecked until an operator with that access completes them.

- [ ] 1.1 Confirm Azure Entra External ID (CIAM) is enabled on the tenant, or create the CIAM tenant if not
- [ ] 1.2 Create the Terraform service principal (Application Administrator + Owner on the target resource group)
- [ ] 1.3 Create the GitHub OIDC federated credential on the Terraform service principal, scoped to this repo + `main`
- [ ] 1.4 Provision the remote state backend (Azure Storage account + container) by hand, with state locking
- [ ] 1.5 Create a `terraform-apply` GitHub Environment with required reviewers (Settings > Environments) so auto-apply-on-merge always waits for human approval

## 2. Terraform foundation (`infra/terraform/`)

- [x] 2.1 Add `providers.tf` (azurerm + azuread) and `backend.tf` pointing at the remote state storage
- [x] 2.2 Reference existing Key Vault, AKS cluster, ACR, and DB resources in `resources.tf` (as data sources — full `terraform import` into managed resource blocks needs an operator with real Azure access to reconcile current config; see README.md)
- [x] 2.3 Add `.github/workflows/terraform-plan.yml` (plan on PR touching `infra/terraform/`, OIDC auth)
- [x] 2.4 Add `.github/workflows/terraform-apply.yml` (apply on merge to `main`, OIDC auth)

## 3. Entra External ID — subscriber auth (`entra.tf`)

- [x] 3.1 Define the CIAM tenant app registration (`azuread_application`) and redirect URIs for the Portal
- [ ] 3.2 Configure the hosted user flow: email/password + email OTP sign-up/sign-in — no Terraform `azuread` provider resource currently covers Entra External ID user flows; this is a manual Graph API/portal step (documented in README.md), not a gap in this PR
- [x] 3.3 Output the CIAM tenant's OIDC discovery URL and client ID for API/Portal config

## 4. Entra ID workforce — admin RBAC (`entra.tf`, `rbac.tf`)

- [x] 4.1 Define the workforce app registration with three app roles: `Admin`, `Operator`, `ReadOnly`
- [x] 4.2 Assign Azure RBAC role assignments per role/resource: `Admin` → Key Vault Secrets Officer + AKS RBAC Admin + ACR Push (resource-group scope); `Operator` → Key Vault Secrets User + AKS RBAC Reader (namespace scope); `ReadOnly` → Reader (resource-group scope)
- [x] 4.3 Document the single break-glass account (subscription Owner, outside Terraform management) in `infra/terraform/README.md`
- [ ] 4.4 Assign current team members (Ryan, Jenn) to their app roles — mechanism is ready (`var.team_role_assignments`); real object IDs go in a not-committed `terraform.tfvars`

## 5. Database migration

- [x] 5.1 Add migration: nullable `entra_oid` column on `subscribers`, unique index
- [ ] 5.2 Add (separate, later) migration: drop `magic_links` table — intentionally held until portal cutover is confirmed live (see task 8)

## 6. API changes (`src/api/NomadRules.Api`)

- [x] 6.1 Update `Program.cs`: point `AddJwtBearer` `Authority`/metadata at the CIAM tenant's OIDC discovery endpoint; remove `Jwt:Secret` config requirement
- [x] 6.2 Add JIT subscriber provisioning: resolve or create `subscribers` row from token `oid` claim on first authenticated request
- [x] 6.3 Remove `AuthService.CreateMagicLinkAsync`/`VerifyMagicLinkAsync` and the `/api/auth/magic-link`, `/api/auth/verify` routes from `AuthEndpoints.cs`
- [x] 6.4 Keep `POST /api/auth/logout` only if still meaningful under token-based auth — removed; MSAL's client-side `logoutRedirect()` replaces it, no server-side session to clear

## 7. Portal changes (`src/portal`)

- [x] 7.1 Add `@azure/msal-browser` + `@azure/msal-react`; configure MSAL with CIAM tenant client ID + authority
- [x] 7.2 Replace `Login.tsx` sign-in/register forms with MSAL redirect-to-hosted-flow
- [x] 7.3 Update `src/portal/src/lib/api.ts`: attach `Authorization: Bearer` from MSAL token acquisition; remove `credentials: 'include'` reliance for auth
- [x] 7.4 Update env config with CIAM client ID and authority (non-secret) — Portal has no Helm deployment; updated `.env.example` instead

## 8. Cutover and cleanup

- [ ] 8.1 Deploy API + Portal changes together behind a communicated deploy window — live deployment action, not doable from this session
- [ ] 8.2 Verify existing subscribers can sign in via Entra and are correctly matched to their `subscribers` row — needs a live CIAM tenant + deployed API
- [ ] 8.3 Once stable, ship the follow-up migration dropping `magic_links` (task 5.2) and delete the now-dead magic-link code paths/tests
- [x] 8.4 Update `CLAUDE.md` principle #8 ("magic links, not OAuth") to reflect the new auth approach
