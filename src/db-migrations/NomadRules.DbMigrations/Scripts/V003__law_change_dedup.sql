-- The ingest consumer keys idempotency on the crawler's messageId. A re-delivered message (Service Bus
-- is at-least-once) or a re-run --run-now must not create a second law_changes row. The UNIQUE index +
-- INSERT OR IGNORE is that guard. Nullable so rows created by other paths (tests, manual) don't collide.
-- See law-change-ingest-bridge design Decision 3.
ALTER TABLE law_changes ADD COLUMN source_message_id TEXT;
CREATE UNIQUE INDEX IF NOT EXISTS ux_law_changes_source_message_id
  ON law_changes(source_message_id) WHERE source_message_id IS NOT NULL;
