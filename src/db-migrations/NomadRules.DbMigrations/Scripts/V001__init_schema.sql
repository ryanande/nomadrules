CREATE TABLE IF NOT EXISTS subscribers (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  state TEXT NOT NULL DEFAULT 'TX',
  insurance_renewal_month INTEGER,
  registration_renewal_month INTEGER,
  license_renewal_month INTEGER,
  tax_due_month INTEGER,
  tier TEXT NOT NULL DEFAULT 'free',
  stripe_customer_id TEXT,
  stripe_subscription_id TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS magic_links (
  token TEXT PRIMARY KEY,
  subscriber_id TEXT NOT NULL REFERENCES subscribers(id),
  expires_at TEXT NOT NULL,
  used INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS law_changes (
  id TEXT PRIMARY KEY,
  source_id TEXT NOT NULL,
  url TEXT NOT NULL,
  raw_content TEXT,
  headline TEXT,
  summary TEXT,
  severity TEXT,
  state TEXT NOT NULL DEFAULT 'TX',
  detected_at TEXT NOT NULL,
  processed_at TEXT
);

CREATE TABLE IF NOT EXISTS notifications (
  id TEXT PRIMARY KEY,
  subscriber_id TEXT NOT NULL REFERENCES subscribers(id),
  law_change_id TEXT NOT NULL REFERENCES law_changes(id),
  delivery_type TEXT,
  sent_at TEXT,
  opened_at TEXT,
  UNIQUE(subscriber_id, law_change_id)
);
