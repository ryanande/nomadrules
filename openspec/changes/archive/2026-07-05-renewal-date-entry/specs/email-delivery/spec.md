## MODIFIED Requirements

### Requirement: Renewal alerts at 60/30/7 days
The system SHALL send a renewal alert to each subscriber at 60, 30, and 7 days before each configured renewal, anchored to the subscriber's renewal month **and day when a day is set, falling back to the 1st of the month when no day is set**.

#### Scenario: Alert due at a trigger offset
- **WHEN** the daily tick runs and a subscriber's anchored renewal date is exactly 60, 30, or 7 days out
- **THEN** the system sends a renewal alert email for that category
- **AND** records a `renewal_alerts` row for `(subscriber, category, offset, year)`

#### Scenario: Day-accurate anchor when a renewal day is set
- **WHEN** a subscriber has both a renewal month and a renewal day for a category
- **THEN** the anchor is that month-and-day (this year, or next year if already passed)
- **AND** the 60/30/7-day triggers are computed against that day-accurate anchor

#### Scenario: Falls back to the 1st when no day is set
- **WHEN** a subscriber has a renewal month but no renewal day for a category
- **THEN** the anchor is the 1st of that month — identical to the prior month-only behavior
- **AND** existing subscribers see no change in when their alerts fire

#### Scenario: Alert is sent exactly once
- **WHEN** the tick runs again the same day (or a retried CronJob fires)
- **THEN** the `UNIQUE(subscriber_id, category, trigger_offset, renewal_year)` constraint prevents a second send
- **AND** no duplicate email is sent

#### Scenario: Renewal already passed this year
- **WHEN** a subscriber's anchored renewal date (month, or month-and-day) is earlier than today
- **THEN** the anchor rolls to the same month-and-day next year
- **AND** triggers are computed against the next-year anchor

#### Scenario: Subscriber has no renewal month for a category
- **WHEN** a subscriber's `*_renewal_month` for a category is NULL
- **THEN** no alert is computed or sent for that category, regardless of any stored day
