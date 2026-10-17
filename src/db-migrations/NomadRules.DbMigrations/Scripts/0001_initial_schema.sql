-- Initial schema: the collapse of the original V001-V007 scripts. Nothing had been deployed when these were
-- folded together, so existing databases must be recreated rather than upgraded (DbUp keys on script name).
-- magic_links is not carried over: magic-link auth was superseded by Entra External ID.

CREATE TABLE subscribers (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  -- NULL until the subscriber picks a domicile during onboarding (JIT-provisioned on first Entra sign-in).
  state TEXT,
  insurance_renewal_month INTEGER,
  registration_renewal_month INTEGER,
  license_renewal_month INTEGER,
  tax_due_month INTEGER,
  -- Day-of-month paired with each *_renewal_month. A NULL day means "anchor to the 1st". Validity (a real
  -- calendar day for the month) is enforced at the subscriber-API boundary before storage.
  insurance_renewal_day INTEGER,
  registration_renewal_day INTEGER,
  license_renewal_day INTEGER,
  tax_due_day INTEGER,
  tier TEXT NOT NULL DEFAULT 'free',
  stripe_customer_id TEXT,
  stripe_subscription_id TEXT,
  -- Entra object id; populated by JIT provisioning on first sign-in (see AuthService).
  entra_oid TEXT,
  created_at TEXT NOT NULL DEFAULT (now()::text),
  updated_at TEXT NOT NULL DEFAULT (now()::text)
);

CREATE UNIQUE INDEX idx_subscribers_entra_oid ON subscribers(entra_oid) WHERE entra_oid IS NOT NULL;

CREATE TABLE law_changes (
  id TEXT PRIMARY KEY,
  source_id TEXT NOT NULL,
  url TEXT NOT NULL,
  raw_content TEXT,
  headline TEXT,
  summary TEXT,
  severity TEXT,
  -- Two-letter state code, or 'US' for federal changes (see 0002 lookup data).
  state TEXT NOT NULL DEFAULT 'TX',
  detected_at TEXT NOT NULL,
  processed_at TEXT,
  -- Summarizer bookkeeping.
  retry_count INTEGER NOT NULL DEFAULT 0,
  reviewed INTEGER NOT NULL DEFAULT 0,       -- 0 = held for manual review
  hallucination INTEGER NOT NULL DEFAULT 0,  -- flagged during manual review
  next_attempt_at TEXT,                      -- set when deferred after a 429
  -- Ingest idempotency: the crawler's messageId is the dedup key (Service Bus is at-least-once), so a
  -- re-delivered message must not create a second row. content_hash/previous_hash are kept for audit.
  source_message_id TEXT,
  content_hash TEXT,
  previous_hash TEXT
);

-- Plain (non-partial) unique index: Postgres treats NULLs as distinct, so rows with a NULL source_message_id
-- never conflict, and ON CONFLICT(source_message_id) can match it without repeating a WHERE clause.
CREATE UNIQUE INDEX ux_law_changes_source_message_id ON law_changes(source_message_id);

CREATE TABLE notifications (
  id TEXT PRIMARY KEY,
  subscriber_id TEXT NOT NULL REFERENCES subscribers(id),
  law_change_id TEXT NOT NULL REFERENCES law_changes(id),
  delivery_type TEXT,
  sent_at TEXT,
  opened_at TEXT,
  UNIQUE(subscriber_id, law_change_id)
);

-- Renewal alerts have no law_change to key on, so notifications' (subscriber, law_change) unique constraint
-- can't dedup them. UNIQUE(subscriber, category, offset, year) is the idempotency guarantee for the
-- 60/30/7-day alert windows.
CREATE TABLE renewal_alerts (
  id TEXT PRIMARY KEY,
  subscriber_id TEXT NOT NULL REFERENCES subscribers(id),
  category TEXT NOT NULL,           -- insurance | registration | license | tax
  trigger_offset INTEGER NOT NULL,  -- 60 | 30 | 7
  renewal_year INTEGER NOT NULL,
  sent_at TEXT,
  UNIQUE(subscriber_id, category, trigger_offset, renewal_year)
);

CREATE TABLE summarizer_costs (
  id TEXT PRIMARY KEY,
  law_change_id TEXT NOT NULL REFERENCES law_changes(id),
  input_tokens INTEGER NOT NULL,
  output_tokens INTEGER NOT NULL,
  cost_usd REAL NOT NULL,
  created_at TEXT NOT NULL
);
