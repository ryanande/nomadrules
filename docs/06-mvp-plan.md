# MVP Plan

## Philosophy

Ship the smallest thing that proves people will pay for this, then automate. The goal of the MVP is **one paying subscriber** — not a perfect system.

---

## Phase 0 — Proof of concept (Weekend 1)

**Goal:** Manually validate the core value proposition before writing a line of production code.

**Tasks:**
- [ ] Pick one state, one category (e.g., SD insurance)
- [ ] Manually check SD Division of Insurance bulletin page
- [ ] Run the raw text through Claude API (via Workbench or API Explorer) with a hand-crafted prompt
- [ ] Write the plain-English summary yourself; share in 2–3 RV Facebook groups or forums
- [ ] Gauge engagement — comments, DMs, "how do I get this regularly?"

**Success criteria:** At least 20 people express interest in receiving this regularly.

**Time estimate:** 1 day

---

## Phase 1 — Manual newsletter (Week 1–2)

**Goal:** Get first paying subscribers before the system is automated.

**Stack:** Beehiiv (free tier) + Claude API via Workbench + Stripe payment link

**Tasks:**
- [ ] Set up Beehiiv newsletter — "NomadRules Weekly"
- [ ] Create Stripe payment link for Basic tier ($9/mo)
- [ ] Manually run 3–5 sources weekly (SD, TX, FL insurance + tax)
- [ ] Use Claude to summarize; paste into Beehiiv; send Friday
- [ ] Post in RV communities with free preview + subscribe link
- [ ] Manually onboard paying subscribers

**Success criteria:** 10 paying subscribers at $9/mo = $90 MRR

**Time estimate:** 1–2 weekends

---

## Phase 2 — Crawler automation (Weeks 3–4)

**Goal:** Eliminate the manual weekly scraping. The newsletter stays manual; only data collection is automated.

**Tasks:**
- [ ] Scaffold TypeScript crawler project (`src/crawler/`)
- [ ] Implement `ISourceScraper` interface
- [ ] Write scrapers for P0 sources (SD, TX, FL — insurance + tax)
- [ ] Deploy to Azure Container Apps with cron trigger
- [ ] Store snapshots in Azure Blob Storage
- [ ] Implement HTML diff engine
- [ ] On diff detected: write to a "review queue" (simple Cosmos container or even a GitHub Issue via API — readable by founder)
- [ ] Friday: review queue → Claude summary → paste to Beehiiv

**Success criteria:** Founder is notified of law changes automatically; no manual checking of government sites

**Time estimate:** 2 weekends

---

## Phase 3 — Automated summarization (Week 5–6)

**Goal:** Claude runs automatically on detected diffs; founder reviews summaries, not raw diffs.

**Tasks:**
- [ ] Add Azure Function: `SummarizationFunction` triggered by blob write or Cosmos change feed
- [ ] Implement Claude API call with summarization prompt
- [ ] Store `LawChange` record in Cosmos DB with headline + summary + severity
- [ ] Build simple admin view (can be a Cosmos Data Explorer query or a minimal React page) to review pending summaries
- [ ] One-click "approve and queue for digest" action

**Success criteria:** Founder reviews plain-English summaries, not raw HTML diffs. Time to produce newsletter drops to <30 min/week.

**Time estimate:** 1–2 weekends

---

## Phase 4 — Self-managing pipeline (Weeks 7–10)

**Goal:** End-to-end automation. Founder does zero work on a normal week; only reviews urgent items or system alerts.

**Tasks:**
- [ ] Introduce NServiceBus — refactor Function triggers to message handlers
- [ ] Implement `SeverityScoringHandler` and `TaggingHandler`
- [ ] Implement `RelevanceMatchingHandler` — match changes to subscriber profiles
- [ ] Implement digest batcher (NServiceBus saga — accumulates items per subscriber per week)
- [ ] Integrate Resend API for automated digest delivery
- [ ] Implement urgent alert bypass (immediate send on `severity = "urgent"`)
- [ ] Stripe webhook handlers for subscription lifecycle
- [ ] Subscriber API (ASP.NET Core) — basic profile management
- [ ] Minimal subscriber portal (React) — onboarding, state selection, notification history

**Success criteria:** System sends correct digest to correct subscribers with no manual intervention for 2 consecutive weeks.

**Time estimate:** 4–6 weekends

---

## Phase 5 — Growth features (Month 3+)

**Goal:** Increase subscriber LTV and unlock B2B revenue.

**Tasks:**
- [ ] Pro tier — multi-state, all categories, urgent alerts
- [ ] Law change archive / searchable library in portal
- [ ] Business tier — team seats, white-label API
- [ ] Affiliate link injection in summaries
- [ ] SEO — state-specific law change landing pages (e.g., `/south-dakota/insurance`)
- [ ] Add remaining P1 sources
- [ ] Partner outreach — Escapees, mail forwarding services, RV insurance brokers

---

## First weekend checklist

If starting today:

```
Friday evening:
  □ Create Beehiiv account
  □ Create Stripe account + $9/mo payment link
  □ Manually check SD insurance bulletin page
  □ Run text through Claude — write first summary
  □ Draft first issue of NomadRules Weekly

Saturday:
  □ Send first issue to personal network + RV community
  □ Post in 3 Facebook groups / Escapees forum
  □ Watch for responses

Sunday:
  □ Follow up with interested people
  □ Scaffold GitHub repo structure
  □ Start crawler TypeScript project
```

---

## Risk log

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Government sites block scrapers | Medium | Rate limit crawlers; use official APIs where available (Federal Register); fallback to RSS |
| AI summary quality is inconsistent | Medium | Prompt refinement; human review queue in early phases |
| Low willingness to pay | Low | Validate manually (Phase 1) before automating |
| Legal grey area — republishing law content | Low | We summarize and link; we do not reproduce full text. Consult an attorney before launch. |
| Source goes offline / changes HTML structure | High (over time) | Alerting on scraper failures; modular scraper design makes fixes fast |
| Competing service launches | Low | Community trust + first-mover advantage; the archive is a durable moat |
