-- Marketing funnel capture: email leads from the public site, and contact-form
-- inquiries. Both are written by anonymous public endpoints, so they carry only
-- what the visitor typed — no FK to subscribers (a lead may never register).

CREATE TABLE leads (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email       TEXT NOT NULL,
    source      TEXT,                       -- which CTA/page captured it (e.g. 'home-hero')
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- One row per distinct email; re-submits are idempotent (see MarketingService).
CREATE UNIQUE INDEX idx_leads_email ON leads(lower(email));

CREATE TABLE contact_messages (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT NOT NULL,
    email       TEXT NOT NULL,
    topic       TEXT,                       -- general | support | press | partnership
    message     TEXT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
