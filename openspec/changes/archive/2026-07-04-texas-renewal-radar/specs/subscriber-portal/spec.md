# Subscriber Portal Specification

## ADDED Requirements

### Requirement: Portal is publicly accessible
The system SHALL host a web portal at nomadrules.com for users to manage profiles and view law changes.

#### Scenario: Portal is accessible
- **WHEN** user navigates to nomadrules.com
- **THEN** portal loads (React app)
- **AND** displays public feed OR login/signup prompts

### Requirement: Calendar onboarding flow
The system SHALL guide new users through entering renewal dates.

#### Scenario: New visitor sees signup flow
- **WHEN** unauthenticated user navigates to nomadrules.com
- **THEN** portal displays onboarding form
- **AND** fields: email, domicile state (dropdown: TX, SD, FL), insurance_renewal_month (1-12), etc.

#### Scenario: Onboarding is mobile-friendly
- **WHEN** user accesses portal on mobile
- **THEN** form fields stack vertically
- **AND** dropdowns are native mobile pickers
- **AND** button is touch-friendly (48px+)

#### Scenario: Onboarding confirms submission
- **WHEN** user submits calendar form
- **THEN** portal displays: "Thanks! Check your email for a confirmation"
- **AND** redirects to login/magic link page after 3 seconds

### Requirement: Dashboard (authenticated)
The system SHALL display personalized dashboard to logged-in users.

#### Scenario: User sees dashboard
- **WHEN** authenticated user logs in
- **THEN** portal displays dashboard with:
  - Recent law changes for their state (last 5)
  - "Your next renewal: Insurance in October (60 days away)"
  - Quick links: "View all changes", "Update profile", "Manage billing"

#### Scenario: Dashboard shows upcoming renewals
- **WHEN** user views dashboard
- **THEN** system displays countdown to next renewal
- **AND** e.g., "🗓️ Insurance renewal in 60 days"
- **AND** includes relevant law changes from past 12 months

### Requirement: Law change archive
The system SHALL allow users to browse and search law changes.

#### Scenario: User can view all changes
- **WHEN** user clicks "View all changes" or navigates to /changes
- **THEN** portal displays paginated list of all law_changes for their state
- **AND** sorted by detected_at descending
- **AND** each item shows: headline, summary, date, "Read more" link

#### Scenario: User can filter by category
- **WHEN** user filters by "Insurance" (or other category)
- **THEN** portal displays only changes in that category
- **AND** (v0.1: insurance-only; filter added in v0.2)

#### Scenario: User can search
- **WHEN** user enters search term in archive
- **THEN** portal searches headlines + summaries
- **AND** returns matching law_changes
- **AND** highlights search term in results

#### Scenario: User can expand full change
- **WHEN** user clicks "Read more" on a law change
- **THEN** portal displays full summary + link to original source
- **AND** shows: source name, URL, detected_at, severity

### Requirement: Profile management
The system SHALL allow users to update email and renewal dates.

#### Scenario: User accesses profile
- **WHEN** logged-in user navigates to /profile
- **THEN** portal displays form with current values:
  - Email (readonly, or "change email" flow)
  - Domicile state
  - Insurance_renewal_month, registration_renewal_month, license_renewal_month, tax_due_month

#### Scenario: User updates dates
- **WHEN** user edits renewal dates and clicks "Save"
- **THEN** portal sends PUT request to API
- **AND** displays confirmation: "Updated!"
- **AND** refreshes dashboard

#### Scenario: User can change email
- **WHEN** user initiates email change
- **THEN** portal sends magic link to new email
- **AND** requires confirmation (click link in email)
- **AND** updates email only after confirmation

### Requirement: Billing management
The system SHALL provide access to subscription management.

#### Scenario: User sees billing info
- **WHEN** logged-in user navigates to /billing
- **THEN** portal displays:
  - Current tier (Basic / Pro / Free)
  - Price and renewal date (if paid)
  - "Manage billing" button

#### Scenario: User upgrades subscription
- **WHEN** user clicks "Upgrade to Pro"
- **THEN** portal redirects to Stripe Checkout or Customer Portal
- **AND** (or payment link for v0.1 MVP)

#### Scenario: User views billing history
- **WHEN** user clicks "View invoices"
- **THEN** portal redirects to Stripe Customer Portal
- **AND** Stripe handles invoice display

### Requirement: Responsive design
The system SHALL work on desktop, tablet, and mobile.

#### Scenario: Desktop layout
- **WHEN** user accesses portal on desktop (1200px+)
- **THEN** two-column layout: sidebar nav + main content

#### Scenario: Mobile layout
- **WHEN** user accesses portal on mobile (375px+)
- **THEN** single-column layout with collapsible menu
- **AND** buttons and inputs are touch-friendly

### Requirement: Accessibility
The system SHALL meet WCAG 2.1 AA minimum standards.

#### Scenario: Navigation is keyboard accessible
- **WHEN** user uses Tab key to navigate
- **THEN** all interactive elements are reachable
- **AND** focus is visible
- **AND** form labels are associated

#### Scenario: Screen reader compatibility
- **WHEN** user uses screen reader
- **THEN** all content is announced correctly
- **AND** form validation errors are announced
- **AND** links have descriptive text (not "click here")

### Requirement: Loading states
The system SHALL display loading indicators during API calls.

#### Scenario: Loading during page transition
- **WHEN** user navigates between pages
- **THEN** portal shows loading spinner
- **AND** prevents double-clicks on buttons

#### Scenario: Loading during update
- **WHEN** user saves profile changes
- **THEN** button shows "Saving..." text
- **AND** button is disabled until response returns

### Requirement: Error handling
The system SHALL display clear error messages.

#### Scenario: API error is handled
- **WHEN** API call fails (500, timeout, etc.)
- **THEN** portal displays user-friendly error: "Something went wrong. Try again or contact support."
- **AND** does NOT display raw error messages

#### Scenario: Validation errors are displayed
- **WHEN** user submits invalid form (missing required field)
- **THEN** portal displays inline error: "Month is required"
- **AND** highlights the problematic field in red

### Requirement: Performance
The system SHALL load quickly.

#### Scenario: Portal loads fast
- **WHEN** user navigates to portal
- **THEN** initial page load is <3 seconds
- **AND** (including React bootstrap, API calls)
- **AND** law change list is infinite-scroll or paginated (not load all 1000 at once)

### Requirement: Logging out
The system SHALL allow users to log out.

#### Scenario: User logs out
- **WHEN** user clicks "Log out" in menu
- **THEN** portal clears authentication cookie
- **AND** redirects to public feed
- **AND** user is prompted to log in again on next portal access

### Requirement: "No subscription" state
The system SHALL clearly display free vs. paid features.

#### Scenario: Free user sees upgrade CTA
- **WHEN** free user views dashboard
- **THEN** displays: "You're on the free tier. Upgrade to Pro to unlock [features]"
- **AND** CTA button: "Upgrade now"

#### Scenario: Paid user sees no CTA
- **WHEN** Pro/Business subscriber views dashboard
- **THEN** displays: "Thanks for your subscription!"
- **AND** no upgrade CTA
