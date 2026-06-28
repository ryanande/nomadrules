# Renewal Calendar Tool Specification

## ADDED Requirements

### Requirement: User can enter renewal dates
The system SHALL provide a public web form where users enter their domicile state and renewal dates for insurance, vehicle registration, driver's license, and taxes.

#### Scenario: Successful calendar completion
- **WHEN** user opens nomadrules.com/calendar
- **THEN** system displays a form with fields for state (dropdown: TX, SD, FL), insurance renewal month, registration renewal month, license renewal month, tax due month
- **AND** each month field is a dropdown (1-12) or clear input

#### Scenario: User submits calendar
- **WHEN** user fills all fields and clicks "Save & Get Started"
- **THEN** system validates that all fields are populated
- **AND** system captures user's email address (separate field or from signup flow)
- **AND** system stores subscriber record: { email, state, insurance_renewal_month, registration_renewal_month, license_renewal_month, tax_due_month }

#### Scenario: User sees confirmation
- **WHEN** calendar is saved
- **THEN** system displays confirmation: "Thanks! We'll send you personalized alerts on [month] for your insurance renewal"
- **AND** system sends confirmation email to captured email address

### Requirement: User can update renewal dates
The system SHALL allow users to update their stored renewal dates after initial signup.

#### Scenario: User accesses profile to update dates
- **WHEN** user clicks "My Dates" or similar link after signup
- **THEN** system displays current renewal dates (pre-populated)
- **AND** user can edit each date
- **AND** user can click "Save Changes"

#### Scenario: Updated dates are persisted
- **WHEN** user updates dates and clicks "Save"
- **THEN** system persists new dates to subscriber record
- **AND** system sends confirmation email: "Your renewal dates are updated"

### Requirement: Public feed (no paywall)
The system SHALL display a public, unauthenticated feed of recent law changes for each state.

#### Scenario: User can access free public feed
- **WHEN** user navigates to nomadrules.com/texas/changes or similar
- **THEN** system displays the 3 most recent insurance law changes for Texas
- **AND** each item shows: headline, state, category, date
- **AND** NO paywall or login required

#### Scenario: Public feed has CTA to subscribe
- **WHEN** user views public feed
- **THEN** each item includes a link: "Get this delivered to your inbox → [Subscribe]"
- **AND** click goes to signup/payment flow

### Requirement: Mobile responsive
The system SHALL work on mobile browsers (iPhone, Android).

#### Scenario: Form displays on mobile
- **WHEN** user opens calendar form on mobile
- **THEN** form fields stack vertically
- **AND** dropdown menus are native mobile dropdowns
- **AND** button is touch-friendly (48px minimum height)

### Requirement: Accessibility
The system SHALL meet WCAG 2.1 AA minimum standards.

#### Scenario: Form has labels and alt text
- **WHEN** user is using screen reader
- **THEN** each input has associated <label>
- **AND** error messages are announced

### Requirement: No auth required for calendar signup
The system SHALL NOT require password or OAuth during calendar signup.

#### Scenario: User signs up with email only
- **WHEN** user enters email in calendar form
- **THEN** system does NOT ask for password
- **AND** system does NOT require OAuth confirmation
- **AND** user is added to email list automatically (confirmation sent)
