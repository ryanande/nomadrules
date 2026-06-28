# Design: Texas Renewal Radar MVP

## Context

We're building a subscription service that alerts full-time RVers and digital nomads to regulatory changes in their domicile state, timed to their renewal calendar. The MVP focuses on Texas, insurance-first (to validate quality before expanding to tax/DMV/voting), and emphasizes a tight feedback loop (ship, measure, iterate).

The full architecture in `docs/02-system-architecture.md` is the target state. This design intentionally simplifies for v0.1 to ship in 2-3 weeks and measure product-market fit before scaling.

**Key constraints:**
- Ship v0.1 in 2-3 weeks (calendar + crawler + delivery)
- Insurance summaries only (tax/voting added after validation)
- Proof of paid conversion before building complex infra
- High iteration velocity (Jenn releases updated copy weekly)
- Ryan owns engineering + DevOps; must be maintainable by one person

## Goals / Non-Goals

**Goals:**
- Deliver personalized law change alerts tied to user's renewal calendar
- Prove product-market fit (100+ free users, 5+ paid, low churn)
- Validate Claude summary quality before multi-category expansion
- Enable B2B testing in parallel (weekly feed = simple white-label distribution)
- Ship faster than overthinking architecture

**Non-Goals:**
- Multi-state (Texas only)
- Tax/DMV/voting in v0.1 (insurance validates the pattern first)
- Sophisticated UI polish (functional MVP)
- Enterprise reliability SLAs (errors okay if we detect + fix fast)
- NServiceBus/saga pattern (overkill for v0.1 scale)

## Decisions

### Decision 1: Crawler Stack — Playwright + Blob Storage + Simple Scheduler

**Chosen:** TypeScript/Node.js + Playwright + local snapshots (Blob Storage or filesystem)

**Alternatives:**
- **Headless browser + Puppeteer**: Heavier, less maintained
- **Request/Cheerio**: Fails on JS-rendered government sites
- **Official APIs**: Gov sites mostly don't have them; Federal Register API exists for IRS but not state-level

**Rationale:**
- Playwright is battle-tested, handles modern government sites
- No SDK complexity; just npm + Docker
- Can run locally for testing before cloud deployment
- Store snapshots simply: blob URI = `snapshots/{state}-{source}-{date}.html`

**Implementation:**
- `src/crawler/` — TypeScript project
- One scraper module per source: `src/crawler/sources/tx-insurance.ts`, etc.
- Each source exports `scrape()` → returns `{ content, hash, url, timestamp }`
- Diff: compare hash against last snapshot; if changed, publish to queue

**Trade-off:** Playwright uses headless Chrome (larger container image), but reliability > size for MVP.

---

### Decision 2: Summarization — Claude API + Prompt Versioning

**Chosen:** Direct HTTP calls to Claude API (Anthropic REST API) from Azure Function

**Alternatives:**
- **Anthropic SDK**: Cleaner, but adds dependency
- **OpenAI**: Not better at legal summaries
- **Local LLM**: Would need GPU, complexity overkill

**Rationale:**
- Direct REST calls give us control over retries, caching, fallback
- Easier to version prompts (check prompt into repo, iterate)
- Cost is known ($0.50/subscriber/month at current volume)
- Can swap prompt without code changes

**Prompts stored as:**
```
src/processor/Prompts/SummarizeInsuranceChange.txt
SummarizeInsuranceChange.txt content:

You are a legal summarizer helping RVers understand insurance regulation changes.
Given raw HTML/text of a change:
1. Extract the headline (one sentence)
2. Write a 2-3 sentence plain-English summary (no jargon)
3. Rate severity: urgent (action needed <30 days) | routine | informational

Format as JSON: {"headline": "...", "summary": "...", "severity": "..."}
```

**Quality gates:**
- Week 2: Manual review of first 5 summaries (Ryan + Jenn)
- Decision point: "Is this good enough?" If not, rewrite prompt or switch to insurance-only
- Monthly: Sample 10% of sent summaries for drift

**Trade-off:** No caching layer (v0.1); can add Redis later. Manual prompt tuning is okay for small volume.

---

### Decision 3: Database — SQLite for v0.1, Migrate to Cosmos Later

**Chosen:** SQLite (local file or in-container) for MVP

**Alternatives:**
- **Azure Cosmos DB**: Serverless, scalable, but overkill for <1000 users
- **PostgreSQL**: More setup, but might be simpler long-term
- **MongoDB**: No native Azure offering, adds cloud dependency

**Rationale:**
- SQLite deploys with the app (no separate DB service)
- File-based backups (just commit SQLite file to git, or backup to blob)
- Query language is identical to production (if we migrate to Postgres)
- Single-file development (npm test runs everywhere)

**Schema:**
```sql
-- subscribers
CREATE TABLE subscribers (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  state TEXT NOT NULL, -- "TX" for v0.1
  insurance_renewal_month INT, -- 1-12
  registration_renewal_month INT,
  license_renewal_month INT,
  tax_due_month INT,
  tier TEXT DEFAULT 'basic', -- basic | pro | business
  stripe_customer_id TEXT,
  created_at DATETIME DEFAULT NOW(),
  updated_at DATETIME DEFAULT NOW()
);

-- law_changes
CREATE TABLE law_changes (
  id TEXT PRIMARY KEY,
  source_id TEXT NOT NULL, -- "tx-insurance-bulletins"
  url TEXT NOT NULL,
  raw_content TEXT,
  headline TEXT,
  summary TEXT,
  severity TEXT, -- "urgent" | "routine" | "informational"
  detected_at DATETIME NOT NULL,
  processed_at DATETIME
);

-- notifications
CREATE TABLE notifications (
  id TEXT PRIMARY KEY,
  subscriber_id TEXT NOT NULL REFERENCES subscribers(id),
  law_change_id TEXT NOT NULL REFERENCES law_changes(id),
  delivery_type TEXT, -- "digest" | "urgent"
  sent_at DATETIME,
  opened_at DATETIME,
  UNIQUE(subscriber_id, law_change_id)
);
```

**Trade-off:** Single-node bottleneck at scale (>10k users). But we'll have years of runway before hitting that.

---

### Decision 4: Queue/Scheduling — Cron + SQLite Queue Table

**Chosen:** Simple cron (Azure Container Apps cron trigger) + SQLite job queue table

**Alternatives:**
- **Azure Service Bus**: Enterprise-grade, but adds management overhead
- **Azure Queue Storage**: Simpler than Service Bus, but still adds service
- **NServiceBus**: Full transactional saga support, but weeks of integration

**Rationale:**
- Crawler runs on fixed cron (e.g., daily 2 AM UTC)
- On diff detected, insert row into `law_changes` table
- Separate function polls `law_changes` where `processed_at IS NULL` → calls Claude → updates `processed_at`
- No distributed transaction complexity; SQLite is ACID
- Easy to debug (query the table, see what's pending)

**Pseudocode:**
```
Crawler runs (daily 2 AM)
  → for each source: scrape + diff
  → if diff: INSERT INTO law_changes (raw_content, headline=NULL, processed_at=NULL)

Summarizer function runs (every 5 min)
  → SELECT * FROM law_changes WHERE processed_at IS NULL LIMIT 10
  → for each: call Claude API → UPDATE law_changes SET headline=..., summary=..., processed_at=NOW()

Delivery runs (Friday 9 AM, or on urgent escalation)
  → SELECT law_changes WHERE processed_at IS NOT NULL and sent_at IS NULL
  → Match to subscribers by renewal month + state
  → INSERT INTO notifications
  → Send via Resend
  → UPDATE notifications SET sent_at=NOW()
```

**Trade-off:** Polling + eventual consistency vs. real-time. But "eventual consistency within 5 min" is fine for regulatory alerts.

---

### Decision 5: Email Delivery — Resend (Free Tier)

**Chosen:** Resend.com SDK

**Alternatives:**
- **SendGrid**: Industry standard, but overkill pricing for MVP
- **AWS SES**: Cheaper, but requires AWS account + DNS setup
- **Beehiiv**: Newsletter platform, but less flexible for programmatic sends

**Rationale:**
- Free tier: 100 emails/day (enough for v0.1)
- Simple SDK
- No spam issues (unlike plain SMTP)
- Easy upgrade path

**Templates:**
- `DigestEmail.tsx` — React component rendering personalized digest
- `UrgentAlertEmail.tsx` — immediate notification
- Both render to HTML + plain text

**Trade-off:** Rate limit on free tier. If we hit 100 daily emails, we upgrade ($1/1000 after that).

---

### Decision 6: Portal — Minimal React App

**Chosen:** React (Vite) on Azure Static Web Apps, minimal features

**MVP features:**
- Onboarding: domicile state selector + renewal calendar form (email capture)
- Dashboard: recent law changes, personalized to their state
- Account: email, renewal dates, tier (readonly; upgrades go to Stripe portal)
- Law archive: searchable list of all changes for their state

**Not in v0.1:**
- Advanced filtering
- Multiple states (locked to Texas)
- Admin dashboard

**Stack:**
- React 19 + TypeScript
- Vite for build
- TanStack Query for API calls (caching + refetch)
- Zustand for state management
- Tailwind CSS
- shadcn/ui for components
- Azure Static Web Apps (free tier, GitHub Actions CI/CD)

**API contract:**
```
POST /api/subscribers — register (email, state, renewal_dates)
GET /api/subscribers/{id}/profile — user details
PUT /api/subscribers/{id}/profile — update dates
GET /api/law-changes?state=TX — feed
POST /webhooks/stripe — Stripe events
```

**Trade-off:** No offline support, no dark mode, bare-bones UX. It's a tool, not a brand experience yet.

---

### Decision 7: Authentication — Email + Token (No OAuth)

**Chosen:** Simple email signup → send magic link → set cookie with JWT token

**Alternatives:**
- **Azure AD B2C**: Over-engineered for MVP
- **Auth0**: Adds cost
- **Passwordless (magic link)**: Simple, user-friendly

**Rationale:**
- No password management burden
- Familiar flow (Stripe sends magic links, works great)
- Can layer in OAuth later if needed

**Implementation:**
- Magic link in email: `nomadrules.com/verify?token={jwt}`
- Token valid for 24 hours
- Set httpOnly cookie on verify
- On API calls, check cookie

**Trade-off:** No built-in session management (we'd add Redis later if needed). For now, cookie is enough.

---

### Decision 8: Deployment — Azure Container Apps (Crawler) + Function (Summarizer) + App Service (API) + Static Web Apps (Portal)

**Chosen:** Microservices-ish (but all serverless, all Azure)

```
User signs up on Portal (Static Web Apps)
  ↓ [HTTP]
API (App Service) handles registration, profile, auth
  ↓ [SQLite]
Crawler (Container App, cron) runs daily
  ↓ [Inserts into SQLite]
Summarizer (Function, polling) reads pending + calls Claude
  ↓ [Updates SQLite]
Delivery (Function, scheduled) sends emails via Resend
```

**Alternatives:**
- **Single App Service**: Monolith, simpler deployment but harder to scale later
- **Docker-compose locally**: Develop locally, push container images

**Rationale:**
- Crawler as Container App: cron triggers built-in, scales to zero
- Summarizer as Function: polling is cheaper than running constantly
- API as App Service: standard for ASP.NET, easy to scale
- Portal as Static Web Apps: free tier, GitHub Actions pipeline

**Terraform structure:**
```
infra/
  main.tf — resource group, managed identities
  crawler.tf — Container Apps environment + cron
  processor.tf — Function App + App Insights
  api.tf — App Service
  portal.tf — Static Web App
  data.tf — SQLite backup to Blob
  variables.tf
```

**Trade-off:** More services = more dashboards, but each service is single-purpose (easier to debug).

---

## Risks / Trade-offs

| Risk | Impact | Mitigation | Timeline |
|------|--------|-----------|----------|
| **Claude hallucination on summaries** | User pays for bad info → refund request → lose trust | Manual QA on first 10; pivot to insurance-only if bad | Week 3 |
| **TX gov sites block Playwright scraper** | No data updates → user complains → bad reputation | Rate limit crawler; use official APIs where available; add alerts on scraper failure | Week 2 |
| **SQLite doesn't scale past 1k users** | Performance degrades; need DB migration | Plan migration to Postgres; start before problem is acute (100 users = safe threshold) | Month 3 |
| **Stripe webhook failures** | User subscribes but isn't marked as paid → access blocked | Webhook retry logic; also check Stripe directly on signup | Week 4 |
| **Renewal dates become stale** | Alerts sent at wrong time; user misses them → churn | Refresh reminder email every 30 days; track staleness in metrics | Week 6 |
| **Free tier cannibalizes paid** | Free users never convert → CAC is negative | Lock free tier to 3 latest items; paid gets full history + search | Week 2 |
| **Resend free tier rate limit** | If we hit 100 emails/day, some digest emails fail | Monitor usage daily; upgrade to paid plan at 50% of limit | Week 4 |
| **Multi-category too complex** | Tax/voting summaries are bad; impacts insurance credibility | Insurance-only in v0.1; add categories only after insurance is proven | Week 2 |

---

## Migration Plan

### Deployment sequence

**Phase 0 (Week 1): Infrastructure**
- Terraform: resource group, managed identities, Static Web Apps
- SQLite schema: create tables locally
- GitHub Actions: test + build pipeline

**Phase 1 (Week 1-2): Calendar + API**
- Portal: React app with onboarding form
- API: /api/subscribers POST (register), GET (profile)
- Static Web Apps: deploy portal
- App Service: deploy API
- Stripe: create product + payment link

**Phase 2 (Week 2): Crawler**
- TX Insurance scraper: Playwright + diff engine
- Container Apps: deploy crawler with cron
- SQLite: insert diffs into law_changes table

**Phase 3 (Week 2-3): Summarizer**
- Claude integration: call API with insurance prompt
- Function App: polling trigger, updates law_changes
- QA: manual read of first 10 summaries

**Phase 4 (Week 3-4): Delivery**
- Digest engine: match law changes to subscribers
- Resend: send weekly digest Friday 9 AM
- Urgent alerts: bypass digest for urgent severity
- Stripe webhooks: handle subscription events

**Phase 5 (Week 4): Go Live**
- Portal public
- Jenn posts in Facebook groups
- Monitor signup rate, crawler health, conversion

### Rollback

- **Portal down**: revert Static Web Apps deployment (GitHub)
- **API errors**: rollback App Service (blue-green deployment)
- **Crawler broken**: disable cron trigger; manual alerts to users
- **Summarizer broken**: queue builds up; manual review (or wait for fix)
- **Database corruption**: restore from blob backup (daily snapshots)

### Monitoring

- Application Insights: all Azure services log here
- Daily metrics: signups, crawler health, API errors, email send rate
- Weekly: manual check of 5 sent emails (quality)
- Alert on: scraper failure, function exception, Stripe error, >1% failed emails

---

## Open Questions

1. **Should we auto-populate renewal dates from TX public vehicle registration data, or keep calendar manual?**
   - Manual: faster to ship (week 1), user owns accuracy
   - Auto: better UX/retention, but requires data integration work (week 2-3)
   - **Recommendation for v0.1**: Manual. Add auto after we validate the renewal-calendar concept works.

2. **Which TX sources in v0.1?**
   - Insurance-only (simplest): TX Division of Insurance bulletins
   - Insurance + federal: add IRS news for tax subscribers (optional)
   - **Recommendation**: Insurance-only. Tax added only if we add tax category.

3. **Stripe: Full Billing Portal vs. Payment Link?**
   - Portal: more professional, but 1-2 weeks to embed correctly
   - Payment link: ship today, upgrade later
   - **Recommendation for v0.1**: Payment link + redirect to Stripe portal for billing management.

4. **Should we monitor all four states (TX/SD/FL) in parallel, or lock to Texas?**
   - All four: broader signal, but Jenn's distribution is Texas-specific
   - Texas-only: tighter feedback loop, easier to prove TAM
   - **Recommendation**: Texas-only for v0.1. Multi-state after we prove consumption in TX.
