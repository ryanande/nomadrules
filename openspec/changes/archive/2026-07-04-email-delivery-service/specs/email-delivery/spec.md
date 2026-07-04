## ADDED Requirements

### Requirement: Renewal alerts at 60/30/7 days
The system SHALL send a renewal alert to each subscriber at 60, 30, and 7 days before each configured renewal, anchored to the 1st of the subscriber's renewal month.

#### Scenario: Alert due at a trigger offset
- **WHEN** the daily tick runs and a subscriber's anchored renewal date is exactly 60, 30, or 7 days out
- **THEN** the system sends a renewal alert email for that category
- **AND** records a `renewal_alerts` row for `(subscriber, category, offset, year)`

#### Scenario: Alert is sent exactly once
- **WHEN** the tick runs again the same day (or a retried CronJob fires)
- **THEN** the `UNIQUE(subscriber_id, category, trigger_offset, renewal_year)` constraint prevents a second send
- **AND** no duplicate email is sent

#### Scenario: Renewal month already passed this year
- **WHEN** a subscriber's renewal month is earlier than the current month
- **THEN** the anchor rolls to the 1st of that month next year
- **AND** triggers are computed against the next-year anchor

#### Scenario: Subscriber has no renewal month for a category
- **WHEN** a subscriber's `*_renewal_month` for a category is NULL
- **THEN** no alert is computed or sent for that category

### Requirement: Weekly digest of law changes
The system SHALL send a weekly digest of processed law changes matched to each subscriber's state.

#### Scenario: Digest on the digest day
- **WHEN** the tick runs on the configured digest day and processed law changes exist for a subscriber's state that were not yet sent to them
- **THEN** the system sends one digest email containing those changes
- **AND** records a `notifications` row per included law change with `delivery_type = 'digest'`

#### Scenario: Digest excludes already-sent changes
- **WHEN** assembling a subscriber's digest
- **THEN** law changes already present in `notifications` for that subscriber are excluded

#### Scenario: No changes this week
- **WHEN** a subscriber has no unsent processed law changes for their state
- **THEN** no digest email is sent

#### Scenario: Only processed changes are included
- **WHEN** assembling a digest
- **THEN** only `law_changes` with `processed_at IS NOT NULL` are eligible
- **AND** raw content is never included in the email

### Requirement: Urgent changes bypass the digest
The system SHALL send urgent-severity law changes as immediate alerts rather than waiting for the weekly digest.

#### Scenario: Urgent change detected
- **WHEN** a processed law change has `severity = 'urgent'` and has not been sent to a matching subscriber
- **THEN** the system sends an immediate urgent alert on the next tick (not waiting for the digest day)
- **AND** records a `notifications` row with `delivery_type = 'urgent'`

#### Scenario: Urgent change not re-sent in digest
- **WHEN** a change was already sent as an urgent alert
- **THEN** it is excluded from that subscriber's weekly digest

### Requirement: Resend delivery with failure handling
The system SHALL send email via the Resend API and handle failures without losing the send.

#### Scenario: Successful send
- **WHEN** an email is dispatched to Resend and Resend returns success
- **THEN** the corresponding row's `sent_at` is set to the send time

#### Scenario: Resend failure
- **WHEN** Resend returns an error or is unreachable
- **THEN** the system logs the failure at ERROR with subscriber and email type
- **AND** leaves `sent_at` unset so the send is retried on the next tick
- **AND** does not crash the worker

#### Scenario: Missing API key
- **WHEN** no `RESEND_API_KEY` is configured
- **THEN** the worker fails fast at startup with a clear fatal message

### Requirement: Idempotent send-and-record
The system SHALL guarantee no subscriber is emailed twice for the same alert or digest item, enforced in the database.

#### Scenario: Row reserved before send
- **WHEN** the system decides to send
- **THEN** it reserves the idempotency row (INSERT with `sent_at` NULL) before calling Resend
- **AND** only proceeds with the send if it won the insert (no existing row)

#### Scenario: Crash between send and mark
- **WHEN** the process crashes after Resend accepts but before `sent_at` is set
- **THEN** the reserved row remains and is retried
- **AND** the risk is a bounded single re-send, logged, not an unbounded loop

### Requirement: Startup migrations and self-check
The system SHALL apply pending migrations on startup and expose a runnable self-check for pure logic.

#### Scenario: Migrations applied on startup
- **WHEN** the worker starts
- **THEN** it applies any pending migrations (including `V002__renewal_alerts.sql`) and fails fast on migration error

#### Scenario: Self-check runs without external dependencies
- **WHEN** the worker is invoked with `--selfcheck`
- **THEN** it validates trigger-date math, digest matching, and idempotency-key construction
- **AND** exits nonzero if any assertion fails
