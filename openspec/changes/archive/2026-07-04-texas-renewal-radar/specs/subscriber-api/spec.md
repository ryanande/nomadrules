# Subscriber API Specification

## ADDED Requirements

### Requirement: User registration endpoint
The system SHALL provide an API endpoint for users to register with email and renewal dates.

#### Scenario: User registers
- **WHEN** user POSTs to `/api/subscribers` with { email, state, insurance_renewal_month, registration_renewal_month, license_renewal_month, tax_due_month }
- **THEN** system validates all fields are present and valid
- **AND** creates subscriber record in database
- **AND** returns 201 Created with { id, email, state, ... }

#### Scenario: Duplicate email is rejected
- **WHEN** user registers with email that already exists
- **THEN** system returns 409 Conflict
- **AND** message: "Email already registered"

#### Scenario: Invalid month is rejected
- **WHEN** user registers with month outside 1-12
- **THEN** system returns 400 Bad Request
- **AND** message: "Month must be 1-12"

### Requirement: Profile endpoint (retrieve)
The system SHALL allow users to retrieve their profile.

#### Scenario: User gets own profile
- **WHEN** authenticated user GETs `/api/subscribers/{id}/profile`
- **THEN** system returns subscriber record: { email, state, renewal_dates, tier, stripe_customer_id }

#### Scenario: Unauthenticated request is rejected
- **WHEN** unauthenticated user GETs `/api/subscribers/{id}/profile`
- **THEN** system returns 401 Unauthorized

### Requirement: Profile endpoint (update)
The system SHALL allow users to update their renewal dates and preferences.

#### Scenario: User updates renewal dates
- **WHEN** authenticated user PUTs `/api/subscribers/{id}/profile` with { insurance_renewal_month: 10 }
- **THEN** system updates only provided fields
- **AND** returns 200 OK with updated profile

#### Scenario: User cannot change email via PUT
- **WHEN** user attempts to update email address
- **THEN** system ignores the email field
- **AND** returns 200 OK with unchanged email

### Requirement: Law change feed endpoint
The system SHALL allow users to retrieve law changes relevant to their state.

#### Scenario: User retrieves feed
- **WHEN** authenticated user GETs `/api/subscribers/{id}/feed?limit=20`
- **THEN** system returns array of law_change records for user's state
- **AND** sorted by detected_at descending (newest first)
- **AND** includes: headline, summary, severity, url, detected_at

#### Scenario: Feed respects pagination
- **WHEN** user requests with `?limit=20&offset=20`
- **THEN** system returns next 20 results
- **AND** includes total_count in response for pagination UI

#### Scenario: Feed includes only processed summaries
- **WHEN** feed is generated
- **THEN** includes only law_changes where processed_at IS NOT NULL
- **AND** excludes raw_content (expensive to transfer)

### Requirement: Stripe webhook endpoint
The system SHALL handle Stripe events (subscription created, updated, deleted).

#### Scenario: Subscription created webhook
- **WHEN** Stripe sends POST to `/webhooks/stripe` with event type `customer.subscription.created`
- **THEN** system updates subscriber: { stripe_customer_id, tier, subscription_id }
- **AND** returns 200 OK to Stripe

#### Scenario: Subscription deleted webhook
- **WHEN** Stripe sends event type `customer.subscription.deleted`
- **THEN** system sets subscriber.tier = "free" (or deletes subscription_id)
- **AND** returns 200 OK

#### Scenario: Webhook signature verification
- **WHEN** Stripe webhook arrives
- **THEN** system verifies webhook signature using Stripe signing secret
- **AND** if invalid, returns 403 Forbidden (rejects unsigned webhooks)

### Requirement: API documentation
The system SHALL provide API documentation via Swagger/OpenAPI.

#### Scenario: Swagger endpoint is available
- **WHEN** user navigates to `/api/swagger`
- **THEN** system returns interactive Swagger UI with all endpoints documented

### Requirement: Error responses
The system SHALL return consistent error responses.

#### Scenario: Generic error format
- **WHEN** API error occurs
- **THEN** system returns JSON: { error: "error_code", message: "Human readable message" }
- **AND** appropriate HTTP status code (400, 401, 500, etc.)

### Requirement: Rate limiting
The system SHALL implement rate limiting to prevent abuse.

#### Scenario: User exceeds rate limit
- **WHEN** user makes >100 requests in 1 minute
- **THEN** system returns 429 Too Many Requests
- **AND** includes Retry-After header

### Requirement: Logging
The system SHALL log all API requests and errors.

#### Scenario: Successful requests are logged
- **WHEN** API call completes
- **THEN** system logs: { method, path, status_code, response_time_ms, user_id }
- **AND** logs to Application Insights

#### Scenario: Errors are logged with context
- **WHEN** API error occurs
- **THEN** system logs: { method, path, status_code, error_message, stack_trace, user_id }
- **AND** stores in Application Insights for debugging

### Requirement: CORS
The system SHALL handle Cross-Origin requests from portal frontend.

#### Scenario: Portal can call API
- **WHEN** React portal (different origin) calls `/api/subscribers`
- **THEN** system returns CORS headers allowing the request
- **AND** includes necessary headers: Access-Control-Allow-Origin, etc.
