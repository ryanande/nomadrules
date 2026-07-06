## ADDED Requirements

### Requirement: Pricing page presents the three tiers
The pricing page SHALL present the Basic, Pro, and Business tiers with the prices, limits, and per-tier feature set defined in `docs/05-subscription-model.md`.

#### Scenario: Visitor compares tiers
- **WHEN** a visitor loads the pricing page
- **THEN** Basic, Pro, and Business tiers are shown with their prices and a feature comparison

#### Scenario: Persona guidance is shown
- **WHEN** a visitor reviews a tier
- **THEN** the tier indicates its target persona (e.g., casual/retired, active full-timer, B2B/professional)

### Requirement: Monthly/annual billing toggle
The pricing page SHALL let the visitor toggle between monthly and annual pricing, and annual pricing SHALL reflect the ~17% (two-months-free) discount.

#### Scenario: Visitor switches to annual
- **WHEN** a visitor toggles from monthly to annual
- **THEN** each tier's displayed price updates to its annual value

### Requirement: Trial messaging and per-tier CTA
The pricing page SHALL communicate the 14-day free trial (no credit card at signup) and provide a call to action per tier that routes into the sign-in / start-trial flow.

#### Scenario: Visitor starts a trial from a tier
- **WHEN** a visitor clicks a tier's call to action
- **THEN** they are routed into the existing sign-in / start-trial flow

#### Scenario: Trial terms are visible
- **WHEN** a visitor views the pricing page
- **THEN** the 14-day free trial with no card required is stated
