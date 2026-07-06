## ADDED Requirements

### Requirement: Public routes render without authentication
The portal SHALL serve the marketing routes (home, product/value detail, pricing, business) to unauthenticated visitors without triggering an MSAL sign-in redirect.

#### Scenario: Anonymous visitor lands on home
- **WHEN** an unauthenticated visitor navigates to `/`
- **THEN** the marketing home page renders and no sign-in redirect occurs

#### Scenario: Anonymous visitor opens a marketing route directly
- **WHEN** an unauthenticated visitor loads `/pricing` or `/business` directly (deep link or refresh)
- **THEN** that marketing page renders correctly without a sign-in redirect

### Requirement: Authenticated app is preserved behind a gated route
The portal SHALL mount the existing authenticated experience (profile, feed) under a gated route prefix that requires an MSAL-authenticated session, preserving the current signed-in behavior.

#### Scenario: Signed-in user reaches the app
- **WHEN** an authenticated user navigates to the gated app route
- **THEN** the profile/feed experience renders as it did before this change

#### Scenario: Anonymous user hits a gated route
- **WHEN** an unauthenticated visitor navigates to a gated app route
- **THEN** they are directed into the sign-in flow rather than shown app content

### Requirement: Marketing CTAs route into the existing auth flow
Sign-in and start-trial calls to action on marketing pages SHALL route into the existing MSAL sign-in flow rather than a bespoke auth path.

#### Scenario: Visitor clicks Sign in
- **WHEN** a visitor clicks a "Sign in" or "Start free trial" CTA
- **THEN** the existing MSAL sign-in flow is invoked
