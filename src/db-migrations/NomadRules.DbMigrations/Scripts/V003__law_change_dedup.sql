-- The ingest consumer keys idempotency on the crawler's messageId. A re-delivered message (Service Bus
-- is at-least-once) or a re-run --run-now must not create a second law_changes row. The UNIQUE index +
-- ON CONFLICT(source_message_id) DO NOTHING is that guard. Nullable so rows created by other paths
-- (tests, manual) don't collide. content_hash/previous_hash are persisted for audit + future content-based
-- dedup. This DbMigrations script is the SOLE owner of these columns — ingest only verifies their presence.
-- See law-change-ingest-bridge design Decision 3.
ALTER TABLE law_changes ADD COLUMN source_message_id TEXT;
ALTER TABLE law_changes ADD COLUMN content_hash TEXT;
ALTER TABLE law_changes ADD COLUMN previous_hash TEXT;
-- Plain (non-partial) unique index: SQLite treats NULLs as distinct, so rows created by other paths with a
-- NULL source_message_id never conflict. A full-column index is required for ON CONFLICT(source_message_id)
-- to match it (a partial index would need the same WHERE clause repeated in every INSERT).
CREATE UNIQUE INDEX IF NOT EXISTS ux_law_changes_source_message_id
  ON law_changes(source_message_id);
