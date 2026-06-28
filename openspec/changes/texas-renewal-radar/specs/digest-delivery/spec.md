# Digest Delivery Specification

## ADDED Requirements

### Requirement: Weekly digest is sent Friday morning
The system SHALL compile and send personalized weekly digests to subscribers every Friday at 9 AM UTC.

#### Scenario: Digest is generated Friday morning
- **WHEN** scheduled trigger fires Friday at 9 AM UTC
- **THEN** system queries law_changes from past 7 days where processed_at IS NOT NULL
- **AND** groups by subscriber (matching state + categories)
- **AND** renders personalized email for each subscriber
- **AND** sends via Resend API

#### Scenario: Digest is personalized
- **WHEN** digest is generated for subscriber
- **THEN** digest includes ONLY law_changes for subscriber's state (TX)
- **AND** includes ONLY categories subscriber cares about (insurance in v0.1)
- **AND** message is tailored: "Hi [email], here's what changed in Texas insurance this week"

#### Scenario: Empty digest is skipped
- **WHEN** no law_changes exist for a subscriber this week
- **THEN** system does NOT send email (skips digest)
- **AND** logs: subscriber_id, week_of, num_changes (0)

### Requirement: Urgent alerts bypass digest
The system SHALL send urgent law changes immediately, not waiting for Friday digest.

#### Scenario: Urgent change is detected
- **WHEN** law_change is processed with severity = "urgent"
- **THEN** system immediately sends email to all matching subscribers
- **AND** does NOT wait for Friday digest
- **AND** subject: "[ALERT] Texas insurance: [headline]"

#### Scenario: Urgent email includes CTA
- **WHEN** urgent alert is sent
- **THEN** email includes: summary + "Learn more" link to law_changes archive
- **AND** CTA button: "View in full context"

### Requirement: Email rendering
The system SHALL render HTML emails with consistent branding and formatting.

#### Scenario: Email has Jenn's voice
- **WHEN** digest email is sent
- **THEN** email includes personalization from Jenn: "Hi [name], I found 3 updates for you this week"
- **AND** tone is conversational, not robotic

#### Scenario: Email is responsive
- **WHEN** email is rendered
- **THEN** HTML works on desktop, mobile, and Outlook clients
- **AND** includes fallback plain-text version

#### Scenario: Each law change item is clear
- **WHEN** digest includes multiple law changes
- **THEN** each item shows: headline + first sentence of summary + "Read more" link
- **AND** items are visually separated

### Requirement: Delivery via Resend
The system SHALL send emails using Resend API.

#### Scenario: Email is sent
- **WHEN** digest or urgent alert is triggered
- **THEN** system calls Resend SDK: send({ to: email, subject, html, text })
- **AND** Resend API returns success response

#### Scenario: Send failure is retried
- **WHEN** Resend API returns error
- **THEN** system retries up to 2 times with exponential backoff
- **AND** if still failing, stores in failed_emails table for manual retry

### Requirement: Unsubscribe link
The system SHALL include unsubscribe link in every email (CAN-SPAM compliance).

#### Scenario: Unsubscribe link is present
- **WHEN** email is sent
- **THEN** footer includes: "Manage preferences or unsubscribe"
- **AND** links to subscriber profile page (where they can delete account)

### Requirement: Tracking
The system SHALL track email opens and clicks (optional).

#### Scenario: Open tracking
- **WHEN** subscriber opens email
- **THEN** system logs: subscriber_id, law_change_ids, timestamp
- **AND** updates notifications table: opened_at = NOW()

#### Scenario: Open rate is measurable
- **WHEN** Jenn wants to measure engagement
- **THEN** she can query: "% of subscribers who opened this week's digest"

### Requirement: Delivery timing respects subscriber preferences
The system SHALL respect user preferences (v0.1: only digest day).

#### Scenario: Digest day can be customized
- **WHEN** user updates profile: { digest_day: "wednesday" }
- **THEN** system sends digest on Wednesday (not Friday) for this subscriber
- **AND** (v0.1: only Friday; multi-day support in v0.2)

### Requirement: Cost monitoring
The system SHALL track email sending costs.

#### Scenario: Cost is logged per email
- **WHEN** email is sent via Resend
- **THEN** system logs: { subscriber_id, email_type, cost_usd, timestamp }
- **AND** Ryan can query: "Total email costs this month"

### Requirement: Failed deliveries are handled
The system SHALL handle bounces and permanent failures.

#### Scenario: Bounce is detected
- **WHEN** Resend reports bounced email
- **THEN** system marks subscriber: email_valid = FALSE
- **AND** stops sending to this email
- **AND** notifies subscriber: "We couldn't deliver to your email, update it here"

### Requirement: Digest includes law change archive link
The system SHALL include a link to browse full law change archive.

#### Scenario: Archive link is included
- **WHEN** digest is rendered
- **THEN** footer includes: "View all changes for Texas [here: nomadrules.com/changes/tx]"
- **AND** link goes to portal law change archive
