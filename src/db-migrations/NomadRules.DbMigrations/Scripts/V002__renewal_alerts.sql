-- Renewal alerts have no law_change to key on, so notifications' (subscriber, law_change) unique
-- constraint can't dedup them. This table's UNIQUE(subscriber, category, offset, year) is the
-- idempotency guarantee for the 60/30/7-day alert windows. See email-delivery design Decision 2.
CREATE TABLE IF NOT EXISTS renewal_alerts (
  id TEXT PRIMARY KEY,
  subscriber_id TEXT NOT NULL REFERENCES subscribers(id),
  category TEXT NOT NULL,           -- insurance | registration | license | tax
  trigger_offset INTEGER NOT NULL,  -- 60 | 30 | 7
  renewal_year INTEGER NOT NULL,
  sent_at TEXT,
  UNIQUE(subscriber_id, category, trigger_offset, renewal_year)
);
