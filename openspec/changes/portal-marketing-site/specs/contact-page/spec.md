## ADDED Requirements

### Requirement: Public contact page with inquiry form
The marketing site SHALL provide a public contact page reachable from the header/footer, with a form capturing name, email, an optional topic/reason, and a message.

#### Scenario: Visitor opens the contact page
- **WHEN** a visitor navigates to the contact route
- **THEN** a contact form (name, email, topic, message) renders without requiring sign-in

#### Scenario: Visitor submits a valid inquiry
- **WHEN** a visitor completes the form with a valid email and message and submits
- **THEN** the inquiry is posted to the backend contact endpoint and a confirmation is shown

#### Scenario: Client-side validation
- **WHEN** a visitor submits with a missing/malformed email or empty message
- **THEN** inline validation messages are shown and no request is sent

### Requirement: Contact form resists spam
The contact form and its endpoint SHALL apply spam mitigation that requires no user friction by default: a hidden honeypot field that must remain empty, and server-side per-IP rate limiting. The design SHALL leave a documented upgrade path to a real captcha (e.g. Cloudflare Turnstile) without a schema change.

#### Scenario: Honeypot-filled submission is rejected
- **WHEN** a submission arrives with the honeypot field populated (bot behavior)
- **THEN** the backend rejects it and does not record an inquiry

#### Scenario: Rapid repeated submissions are throttled
- **WHEN** submissions from the same source exceed the rate limit
- **THEN** further submissions are rejected until the window resets
