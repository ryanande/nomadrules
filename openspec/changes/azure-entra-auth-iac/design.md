## Context

Today: subscriber auth is a magic-link → HMAC-signed JWT in an httpOnly cookie (`src/api/NomadRules.Api/Features/Auth/`); there is no admin/team identity or access model; and there is no cloud IaC — `infra/` holds only Helm templates for the K8s workloads. Azure resources (Key Vault, AKS, ACR, DB) are assumed to already exist but are managed by hand.

This change introduces two identity planes and the IaC to provision both:
1. **Consumer plane** — subscribers signing into the Portal.
2. **Workforce plane** — Ryan/Jenn (and future team) accessing Azure resources and any admin tooling.

Azure Entra has two distinct products for these, and conflating them is the most common mistake in this space, so the design leads with that split.

## Goals / Non-Goals

**Goals:**
- Replace custom magic-link/JWT with Azure Entra External ID (CIAM) for subscriber auth, issued tokens validated by the API via standard OIDC JWT bearer middleware.
- Model least-privilege admin/team access as Entra ID app roles → Azure RBAC role assignments, no standing Owner/Contributor grants.
- Stand up `infra/terraform/` as the single source of truth for every Entra app registration, app role, and RBAC assignment this change introduces, plus the Azure resources those roles scope to.
- CI-driven plan/apply with no long-lived cloud secrets stored in GitHub (federated OIDC).

**Non-Goals:**
- Multi-tenant B2B / organizational subscriber login (consumer plane is CIAM local-account + social, not federated orgs) — not a current requirement.
- Migrating existing Helm-deployed K8s workload manifests to Terraform — Helm keeps owning workload deployment; Terraform owns identity/IAM and the cloud resources Helm's workloads depend on (Key Vault, ACR, AKS cluster, DB).
- Retroactive Terraform import of every pre-existing Azure resource in the subscription — only resources this change touches (Key Vault, AKS, ACR, DB) are brought under management; a full estate import is a separate effort.
- Building admin tooling/UI itself — `admin-access-control` defines the roles and grants; there is no admin portal to wire them into yet.

## Decisions

**Decision: Two separate Entra tenants/products — External ID (CIAM) for subscribers, workforce Entra ID for team**
- Why: Azure Entra External ID (CIAM) is purpose-built for consumer sign-up/sign-in (local accounts, social IdPs, self-service password reset, per-consumer token claims) and is billed per monthly active user. Workforce Entra ID is for known employees/team members with org-directory-backed identity and is where Azure RBAC role assignments naturally live (RBAC principals are workforce-tenant objects). Mixing the two forces either exposing internal RBAC surface to consumers or bolting consumer self-service onto a directory meant for employees.
- Alternative considered: Single Entra ID tenant with a "customers" app registration — rejected; loses CIAM's hosted sign-up/reset UX and per-consumer isolation, and still requires the RBAC split conceptually.

**Decision: API validates Entra tokens directly; no custom-minted JWT**
- Why: `AddJwtBearer` already exists in `Program.cs`; swapping `IssuerSigningKey`/`Authority` to point at the CIAM tenant's OIDC discovery endpoint removes the entire `AuthService.CreateMagicLinkAsync`/`VerifyMagicLinkAsync` surface and the shared-secret (`Jwt:Secret`) that has to be managed and rotated. Subscriber identity claim becomes the Entra `oid`/`sub`, mapped to the existing `subscribers.id` on first login (JIT provisioning).
- Alternative considered: Keep magic-link as a fallback/secondary method — rejected for v1; adds two auth paths to test and reason about for marginal benefit, revisit only if CIAM's email deliverability or UX proves a blocker (pivot trigger, not default).

**Decision: Portal uses MSAL (`@azure/msal-browser` + `@azure/msal-react`) with authorization-code + PKCE, not the current cookie handoff**
- Why: Standard, maintained library for SPA + Entra; PKCE is the current best practice for public clients (no client secret in the browser). Token is held in memory/session by MSAL and attached as `Authorization: Bearer` — replaces `credentials: 'include'` cookie reliance in `src/portal/src/lib/api.ts`.
- Trade-off: Portal must handle token refresh/redirect flows MSAL manages; slightly more client-side complexity than a cookie, but removes the API's responsibility for session/token minting entirely.

**Decision: Admin/team RBAC as Entra ID app roles mapped 1:1 to Azure built-in roles, scoped per-resource**
- Roles: `Admin` (Key Vault Secrets Officer + AKS RBAC Admin + ACR Push, subscription-resource-group scope), `Operator` (Key Vault Secrets User + AKS RBAC Reader, namespace scope), `ReadOnly` (Reader, resource-group scope). No custom RBAC role definitions in v1 — built-ins cover the three tiers needed.
- Why: Built-in roles are reviewed by Microsoft, well-documented, and least-privilege composable; custom role definitions are a maintenance burden with no payoff until a built-in role is proven too broad for a specific need.
- Alternative considered: PIM (Privileged Identity Management) just-in-time elevation for `Admin` — good idea, deferred: requires Entra ID P2 licensing, evaluate once the team grows past two people.

**Decision: Terraform providers `azurerm` + `azuread`, single root module under `infra/terraform/`, remote state in Azure Storage**
- Why: `azuread` provider is required for Entra app registrations/app roles (not covered by `azurerm`); both are needed in this one root since app roles and their RBAC assignments are logically one change set. Remote state (not local) because CI applies it — local state would be lost between runs.
- Structure: `infra/terraform/{providers.tf, entra.tf, rbac.tf, resources.tf, variables.tf, backend.tf}` — flat, not modules-of-modules; this is one team's infra, module indirection buys nothing yet (revisit if a second environment/region is added).

**Decision: CI authenticates to Azure via GitHub OIDC federated credential, not a stored client secret**
- Why: Removes a long-lived credential from GitHub Actions secrets entirely — the federated credential trusts GitHub's OIDC token for this specific repo+branch, Azure issues a short-lived token per run. This is the same "no standing credential" principle the RBAC design applies to humans, applied to CI.
- Bootstrap chicken-and-egg: the federated credential and the Terraform service principal itself must be created once, by hand (or via a bootstrap script), by whoever has Application Administrator + Owner today — this one step is explicitly outside Terraform's reach and documented as a manual prerequisite in tasks.md.

## Risks / Trade-offs

- [Entra External ID (CIAM) is a newer Microsoft product with less battle-testing than Auth0] → Mitigation: `docs/04-tech-stack.md` already flagged Auth0 as an alternative; if CIAM proves rough during implementation, the JWT-bearer swap in the API is the same shape for either provider — pivot cost is mostly in the Terraform/`azuread` layer and MSAL config, not the API code.
- [Breaking change wipes all existing magic-link sessions and the `magic_links` table] → Mitigation: ship behind a deploy window communicated to the (currently small) subscriber base; existing `subscribers` rows are untouched, only the auth method changes, so no data loss — just a forced re-login.
- [Terraform-managed RBAC could lock out a human if a role assignment is misconfigured] → Mitigation: the bootstrap service principal (and the human who ran bootstrap) retains subscription Owner outside Terraform's management as a break-glass path; never remove the last standing admin via a Terraform apply that hasn't been planned and reviewed.
- [Azure RBAC role assignment propagation can take several minutes] → Mitigation: known Azure behavior, not a bug; document in tasks.md so `terraform apply` completing isn't mistaken for immediate access.
- [CIAM billing is per-MAU] → Mitigation: at current subscriber counts (pre-100 users) this is negligible; revisit only if it becomes a real cost line (pivot-trigger-adjacent, not a blocker now).

## Migration Plan

1. Manual bootstrap (outside Terraform): create the Terraform service principal + GitHub OIDC federated credential, grant it Owner on the target resource group.
2. `terraform apply` — provisions CIAM tenant app registration + user flow, workforce app roles, RBAC assignments, and Key Vault/ACR/AKS/DB resource references (import existing ones by ID first).
3. API: swap `AddJwtBearer` config to Entra `Authority`; deploy behind the existing Helm release (env var change, no schema impact yet).
4. DB migration: add nullable `entra_oid` column to `subscribers`, backfill-on-login (JIT); drop `magic_links` table in a follow-up migration once cutover is confirmed stable (not same deploy — keep rollback cheap).
5. Portal: ship MSAL-based `Login.tsx` behind the same deploy; old `/api/auth/magic-link` and `/verify` endpoints removed once portal cutover is confirmed in prod.
6. Rollback: revert the API config + portal deploy (previous Helm release); `magic_links` table and old endpoints are only removed in step 4/5's follow-up, so rollback within that window needs no data restore.

## Open Questions

- Does the existing Azure subscription already have Entra External ID (CIAM) enabled, or does that require a separate tenant creation step before Terraform can target it? (Blocks step 1 of migration plan — confirm before `/opsx:apply`.)
- Social identity providers (Google/Apple) for subscriber sign-in: in scope for this change, or email/password-only for v1? Defaulting to **email/password + email OTP only** for v1 to keep the CIAM user-flow config small; social IdPs are additive later.
- Team size beyond Ryan/Jenn: the three-role model (`Admin`/`Operator`/`ReadOnly`) assumes it stays small; revisit if headcount grows enough that role sprawl starts.
