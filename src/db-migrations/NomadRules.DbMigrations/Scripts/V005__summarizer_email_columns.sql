-- Folds in what NomadRules.Summarizer/Infrastructure/Migrations.cs and
-- NomadRules.EmailDelivery/Infrastructure/Migrations.cs previously applied via ad-hoc
-- ALTER TABLE at each service's own startup — both files' own comments already flagged
-- this as a stopgap ("swap for DbUp when migrations land"). DbUp is now the sole schema
-- owner; both self-migration files are deleted as part of this change.
ALTER TABLE law_changes ADD COLUMN retry_count INTEGER NOT NULL DEFAULT 0;
ALTER TABLE law_changes ADD COLUMN reviewed INTEGER NOT NULL DEFAULT 0;      -- 0 = held for manual review
ALTER TABLE law_changes ADD COLUMN hallucination INTEGER NOT NULL DEFAULT 0; -- flagged during manual review
ALTER TABLE law_changes ADD COLUMN next_attempt_at TEXT;                    -- set when deferred after a 429

CREATE TABLE IF NOT EXISTS summarizer_costs (
  id TEXT PRIMARY KEY,
  law_change_id TEXT NOT NULL REFERENCES law_changes(id),
  input_tokens INTEGER NOT NULL,
  output_tokens INTEGER NOT NULL,
  cost_usd REAL NOT NULL,
  created_at TEXT NOT NULL
);
