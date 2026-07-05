# law-change-ingest Specification

## Purpose
Consumes `LawChangeDetected` messages from the crawler's queue (local-file dev / Azure Service Bus prod) and persists each as a raw `law_changes` row for the summarizer — exactly once per detected change via a `source_message_id` unique index, ack-after-commit, with malformed messages dead-lettered rather than crashing the worker. This is the bridge that connects the crawler to the rest of the pipeline.

## Requirements
### Requirement: Consume detected changes into law_changes
The system SHALL consume `LawChangeDetected` messages from the crawler's queue and persist each as a `law_changes` row with `raw_content` set and `processed_at` NULL, ready for the summarizer.

#### Scenario: Message ingested as a raw row
- **WHEN** a `LawChangeDetected` message is available on the queue
- **THEN** the system inserts a `law_changes` row with `source_id`, `url`, `raw_content`, `state`, and `detected_at` from the message
- **AND** `processed_at` is left NULL so the summarizer will pick it up
- **AND** `headline`, `summary`, and `severity` are left NULL

#### Scenario: Field mapping from the message contract
- **WHEN** mapping a message to a row
- **THEN** `raw_content` comes from the message `rawContent`, `detected_at` from `detectedAt`, and `state` from the message `state` (defaulting to the MVP state when absent)
- **AND** the row `id` is a new system-owned identifier, not the producer's `messageId`

### Requirement: Exactly-once persistence under redelivery
The system SHALL persist at most one `law_changes` row per detected change, even when a message is delivered more than once.

#### Scenario: Duplicate message is a no-op
- **WHEN** a message with a `messageId` already recorded in `source_message_id` is consumed again
- **THEN** no second row is inserted
- **AND** the consume completes successfully (the duplicate is acknowledged, not retried forever)

#### Scenario: Insert then acknowledge
- **WHEN** the system consumes a message
- **THEN** it inserts the row before acknowledging/removing the message from the queue
- **AND** a crash after commit but before acknowledgement results in a redelivery that is de-duplicated, not a second row

### Requirement: Transport selected by environment
The system SHALL read from a local-file queue in development and Azure Service Bus in production, selected by environment configuration, mirroring the crawler's publisher selection.

#### Scenario: Local-file transport in development
- **WHEN** the transport is configured as local
- **THEN** the system reads message files from the configured local queue directory
- **AND** removes each file only after its row is committed

#### Scenario: Service Bus transport in production
- **WHEN** the transport is configured as Service Bus
- **THEN** the system receives messages from the configured Service Bus queue
- **AND** completes each message only after its row is committed

### Requirement: Malformed messages do not halt ingestion
The system SHALL isolate a malformed or unprocessable message so it does not block the rest of the queue or crash the worker.

#### Scenario: Malformed message
- **WHEN** a message cannot be parsed into the expected contract
- **THEN** the system logs the failure at ERROR with the message identifier
- **AND** sets the message aside (skips the local file / dead-letters the Service Bus message)
- **AND** continues consuming subsequent messages

### Requirement: Startup migrations and self-check
The system SHALL apply pending migrations on startup and expose a runnable self-check for its pure logic.

#### Scenario: Migrations applied on startup
- **WHEN** the worker starts
- **THEN** it applies any pending migrations (including the dedup column/index) and fails fast on migration error

#### Scenario: Self-check runs without external dependencies
- **WHEN** the worker is invoked with a self-check flag
- **THEN** it validates the message-to-row mapping and the dedup-key construction
- **AND** exits nonzero if any assertion fails

