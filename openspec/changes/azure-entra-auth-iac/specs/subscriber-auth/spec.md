## ADDED Requirements

### Requirement: Subscriber sign-in via Entra External ID
The system SHALL authenticate subscribers using Azure Entra External ID (CIAM) hosted sign-in/sign-up, and SHALL NOT mint its own session token.

#### Scenario: New subscriber registers
- **WHEN** a visitor completes the Entra External ID hosted sign-up flow (email + password or email OTP)
- **THEN** the API creates a `subscribers` row on first authenticated request, keyed by the Entra `oid` claim
- **AND** no `magic_links` row or custom JWT is created

#### Scenario: Existing subscriber signs in
- **WHEN** a subscriber with a previously seen Entra `oid` completes sign-in
- **THEN** the API resolves the existing `subscribers` row by `entra_oid`
- **AND** no new subscriber row is created

#### Scenario: Magic-link endpoints are removed
- **WHEN** a client calls `POST /api/auth/magic-link` or `GET /api/auth/verify`
- **THEN** the API returns 404 (routes no longer exist)

### Requirement: API validates Entra-issued tokens
The API SHALL validate bearer tokens against the Entra External ID tenant's OIDC metadata (issuer, signing keys, audience) and SHALL reject tokens that fail validation.

#### Scenario: Valid Entra token
- **WHEN** a request includes a bearer token issued by the configured Entra External ID tenant with a valid signature, issuer, audience, and unexpired lifetime
- **THEN** the API accepts the request and resolves the subscriber from the token's `oid` claim

#### Scenario: Expired or malformed token
- **WHEN** a request includes an expired, malformed, or wrong-audience token
- **THEN** the API returns 401 Unauthorized

#### Scenario: No shared-secret validation path remains
- **WHEN** the API starts up
- **THEN** it SHALL NOT read or require a `Jwt:Secret` configuration value

### Requirement: Portal authenticates via MSAL
The Portal SHALL acquire and attach Entra-issued tokens using MSAL (authorization code + PKCE), and SHALL NOT rely on an httpOnly session cookie for subscriber auth.

#### Scenario: Unauthenticated portal request redirects to Entra
- **WHEN** an unauthenticated user opens a page requiring auth
- **THEN** MSAL redirects to the Entra External ID hosted sign-in page

#### Scenario: Authenticated API calls carry a bearer token
- **WHEN** the Portal calls a protected API endpoint after sign-in
- **THEN** the request includes `Authorization: Bearer <token>` acquired via MSAL
- **AND** the request does not depend on `credentials: 'include'` cookie forwarding for auth
