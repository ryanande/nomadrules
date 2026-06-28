# Renewal Radar Specification

## ADDED Requirements

### Requirement: Renewal-triggered alerts
The system SHALL send personalized alerts when a user's renewal date is approaching, highlighting law changes relevant to that renewal.

#### Scenario: Alert sent 60 days before renewal
- **WHEN** scheduled trigger runs (daily at 3 AM UTC)
- **THEN** system queries subscribers where insurance_renewal_month matches (today + 60 days)
- **AND** finds all law_changes for their state detected in past 12 months
- **AND** sends email: "Your insurance renewal is in 60 days. Here's what changed since last year [list]"

#### Scenario: Alert sent 30 days before renewal
- **WHEN** scheduled trigger runs and renewal is 30 days away
- **THEN** system sends a second alert: "30 days to your renewal — don't miss these changes"
- **AND** includes tracking: renewal_alert_sent_at = NOW()

#### Scenario: Alert sent 7 days before renewal
- **WHEN** renewal is 7 days away
- **THEN** system sends final alert: "1 week to your renewal — here's your quick summary"
- **AND** includes urgent CTA: "Review full changes before renewal"

#### Scenario: Only one alert per renewal per distance
- **WHEN** alert has already been sent 60 days before renewal
- **THEN** next run does NOT send duplicate 60-day alert
- **AND** system tracks in notifications table: { subscriber_id, renewal_type: "insurance", distance_days: 60, sent_at }

### Requirement: Renewal-triggered payment CTA
The system SHALL include a payment/upgrade CTA in renewal alerts, positioned at moment of need.

#### Scenario: Free user gets upgrade CTA
- **WHEN** renewal alert is sent to free subscriber
- **THEN** CTA includes: "Upgrade to Pro ($9/mo) for urgent alerts + multi-state tracking"
- **AND** links to Stripe payment page

#### Scenario: Basic user gets Pro upgrade CTA
- **WHEN** Basic tier subscriber receives renewal alert
- **THEN** CTA includes: "Upgrade to Pro for $9/mo (you're currently on Basic)"

#### Scenario: Pro user sees confirmation
- **WHEN** Pro tier subscriber receives renewal alert
- **THEN** alert thanks them for subscription
- **AND** no upgrade CTA

### Requirement: Historical context in renewal alerts
The system SHALL include summary of what changed since last renewal.

#### Scenario: Alert shows changes from past 12 months
- **WHEN** renewal alert is generated
- **THEN** system queries law_changes for past 12 months (or since last renewal if tracked)
- **AND** includes top 5 changes in email
- **AND** formats as: "Last year at this time, [3 things changed]. This year: [new things]"

### Requirement: Renewal calendar matching
The system SHALL accurately match renewal dates to trigger alerts.

#### Scenario: Renewal month is correctly identified
- **WHEN** subscriber has insurance_renewal_month = 10 (October)
- **THEN** alert is sent when current month + 60 days = October
- **AND** e.g., if today is Aug 1, alert sent (Aug 1 + 60 days ≈ Oct 1)

#### Scenario: Day-of-month is handled
- **WHEN** subscriber has insurance_renewal_month = 3 (March)
- **AND** renewal specific day is NOT tracked (v0.1)
- **THEN** alert is sent on 1st day of renewal month - 60 days

### Requirement: Renewal type specificity
The system SHALL distinguish between insurance, registration, license, and tax renewals (v0.1: insurance only).

#### Scenario: Insurance renewal alert in v0.1
- **WHEN** alert is triggered
- **THEN** alert focuses on insurance-related changes ONLY
- **AND** registration/license/tax changes are excluded (v0.1)

#### Scenario: Future multi-renewal support
- **WHEN** registration renewal is 90 days away
- **THEN** system sends separate alert: "Your vehicle registration renews in 90 days..."
- **AND** (v0.2+ only; v0.1 insurance-only)

### Requirement: Unsubscribe from renewal alerts
The system SHALL allow users to disable renewal alerts.

#### Scenario: User disables renewal alerts
- **WHEN** user updates profile: { renewal_alerts_enabled: false }
- **THEN** system does NOT send 60/30/7 day alerts
- **AND** still sends weekly digest (if subscribed)

### Requirement: Renewal alert delivery via Resend
The system SHALL send renewal alerts using Resend API.

#### Scenario: Renewal alert is sent
- **WHEN** renewal alert is triggered
- **THEN** system calls Resend: send({ to: email, subject: "Your insurance renewal in 60 days...", html, text })
- **AND** Resend returns success

### Requirement: Tracking renewal alert engagement
The system SHALL log which subscribers receive renewal alerts and conversion.

#### Scenario: Renewal alert is tracked
- **WHEN** alert is sent
- **THEN** system inserts into notifications table: { subscriber_id, law_change_id, delivery_type: "renewal_alert", sent_at, distance_days: 60 }
- **AND** Ryan can query: "How many subscribers received renewal alerts this month?"

#### Scenario: Renewal alert conversion is tracked
- **WHEN** free user upgrades after renewal alert
- **THEN** system logs: { subscriber_id, trigger: "renewal_alert", converted_at, tier_upgraded_to }
- **AND** Jenn can measure: "Did renewal alerts drive conversions?"

### Requirement: Edge case: renewal date in the past
The system SHALL handle subscribers who enter a renewal date that has already passed.

#### Scenario: Past renewal date is handled
- **WHEN** subscriber's renewal month is in the past
- **THEN** system assumes next occurrence of that month
- **AND** e.g., if insurance_renewal_month = 3 and current month is 6, next renewal is March of next year
