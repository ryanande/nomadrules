-- Nullable + unique: backfilled via JIT provisioning on first Entra sign-in
-- (see AuthService), not backfilled in bulk here, since it's a 1:1 mapping the
-- API can populate the moment a subscriber first authenticates via Entra.
ALTER TABLE subscribers ADD COLUMN entra_oid TEXT;
CREATE UNIQUE INDEX IF NOT EXISTS idx_subscribers_entra_oid ON subscribers(entra_oid) WHERE entra_oid IS NOT NULL;
