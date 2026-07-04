## Context

The pipeline produces summarized law changes but delivers nothing. This service is the last hop: it reads the shared SQLite schema, decides who should receive what and when, and sends via Resend. It reuses the worker patterns proven in `NomadRules.Summarizer` (Generic Host, polling worker, `Db` + repository, fail-fast migrations, `--selfcheck`) and the observability patterns from `NomadRules.DbMigrations` (Serilog, best-effort telemetry).

Two schema realities shape the design:
- `subscribers.*_renewal_month` is an **INT (1-12)**, not a date. Precise 60/30/7-day triggers need a date.
- `notifications` requires a non-null `law_change_id` and is unique on `(subscriber_id, law_change_id)` — perfect for digest dedup, but renewal alerts have no law change to reference.

## Goals / Non-Goals

**Goals:**
- Renewal alerts fire at 60/30/7 days before each subscriber's renewal, exactly once per (subscriber, category, offset, year)
- Weekly digest of processed law changes matched by state, exactly once per (subscriber, law_change)
- Urgent law changes bypass the digest as immediate alerts
- No subscriber is ever double-emailed (idempotency enforced in the DB, not app memory)
- Resend failures are logged and retried on the next tick, never silently dropped
- Insurance-only, Texas-only for MVP (matches the crawler/summarizer scope)

**Non-Goals:**
- HTML email template polish (Jenn owns copy/design later — plain, functional templates for v0.1)
- Multi-state / multi-category matching beyond insurance + TX
- Open/click tracking beyond what Resend gives for free
- A separate scheduler service (the worker self-schedules via a daily tick)
- Full renewal-date entry (month-only for MVP; see Decision 1)

## Decisions

### Decision 1: Anchor month-only renewals to the 1st of the month

**Chosen:** Treat `insurance_renewal_month = M` as a renewal on the 1st of month M (this year, or next year if that date has already passed). Compute 60/30/7-day offsets from that anchor.

**Alternatives:**
- Block on adding full renewal-date columns first — delays the wedge
- Skip day-precision and send "your renewal is this month" on the 1st — loses the 60/30/7 ROI framing

**Rationale:** The wedge is validated by *timed* alerts, not by day-perfect precision. Anchoring to the 1st gives a defensible 60/30/7 cadence today. When we add full-date entry (a subscriber-API change), only the anchor computation changes — trigger logic and idempotency are unaffected.

**Trade-off:** A subscriber whose real renewal is the 20th gets alerts ~19 days early. Acceptable for MVP; the alert says "around <Month>", not a false exact date.

---

### Decision 2: Dedicated `renewal_alerts` table for alert idempotency

**Chosen:** New migration `V002__renewal_alerts.sql`:
```sql
CREATE TABLE renewal_alerts (
  id TEXT PRIMARY KEY,
  subscriber_id TEXT NOT NULL REFERENCES subscribers(id),
  category TEXT NOT NULL,        -- insurance | registration | license | tax
  trigger_offset INTEGER NOT NULL, -- 60 | 30 | 7
  renewal_year INTEGER NOT NULL,
  sent_at TEXT,
  UNIQUE(subscriber_id, category, trigger_offset, renewal_year)
);
```

**Alternatives:**
- Make `notifications.law_change_id` nullable + add `alert_type` — overloads one table with two unrelated concepts; the unique constraint no longer means one thing
- Track sent alerts in app memory — lost on restart, breaks idempotency across ticks/pods

**Rationale:** The `UNIQUE` constraint is the idempotency guarantee. A dedicated table keeps "did we alert this subscriber for this renewal window this year" a single clean question. Digest sends stay in `notifications` where the `(subscriber, law_change)` unique key already fits.

---

### Decision 3: Resend via direct HTTP, not the SDK

**Chosen:** Direct `HttpClient` POST to `https://api.resend.com/emails`

**Rationale:** Matches the summarizer's direct-Claude decision — control over retries/timeouts, one fewer dependency, and the Resend send API is a single JSON POST. `RESEND_API_KEY` from env/config.

**Trade-off:** We hand-model the tiny request/response. Trivial for one endpoint.

---

### Decision 4: Single polling worker with a daily tick

**Chosen:** One `HostedService` that wakes on an interval (default daily), and on each tick: (a) computes due renewal alerts, (b) picks up urgent law changes for immediate send, (c) on the digest day (Friday) assembles + sends weekly digests. A `--run-now` flag forces one immediate pass for local testing and for a K8s CronJob invocation.

**Alternatives:**
- Separate services for alerts vs digest — three deployments for one concern; premature
- External scheduler (Hangfire/Quartz) — overkill; the DB already records what's been sent

**Rationale:** Idempotency lives in the DB, so "run more than once" is safe — the worker can be a long-running Deployment *or* a CronJob calling `--run-now`. Ponytail: one worker, DB-backed dedup, no scheduler service until throughput demands it.

**Trade-off:** Digest "day/time" is checked in code, not cron-enforced. Fine at this scale; a CronJob makes it precise in prod.

---

### Decision 5: Reuse observability + fail-fast patterns verbatim

**Chosen:** Serilog (JSON prod / console dev, trace-enriched), `Migrations.Apply` on startup with fatal exit on failure, `--selfcheck` for pure logic (trigger-date math, digest matching, idempotency key), best-effort telemetry that never blocks a send.

**Rationale:** Consistency across services beats novelty. The migration runner and summarizer already established these; copy them.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Resend free tier is 100 emails/day | Log daily send count as a metric; alert at 50%. Batch digest sends; renewal alerts are low-volume. |
| Double-send on concurrent pods / retried CronJob | DB `UNIQUE` constraints on `renewal_alerts` and `notifications` — insert-then-send, or send-then-mark with the row as the guard. Use `INSERT OR IGNORE` and only send if the insert won a row. |
| Month-anchored dates alert too early (Decision 1) | Copy says "around <Month>"; upgrade to full dates is isolated to the anchor function. |
| Resend send succeeds but process crashes before marking sent | Small double-send window. Mitigation: reserve the row (`INSERT` sent_at=NULL) before send, set `sent_at` after; a crashed-mid-send row is retried and Resend dedups on idempotency key if we pass one. |
| Empty digest (no law changes that week) | Skip send entirely — no "nothing happened" emails (erodes trust + wastes quota). |

## Migration Plan

1. Add `V002__renewal_alerts.sql` to the migration project
2. Scaffold `NomadRules.EmailDelivery` worker (copy summarizer patterns)
3. Implement trigger-date math + digest matching (`--selfcheck` covers these)
4. Implement Resend client (direct HTTP)
5. Wire idempotent send-and-record for both alert and digest paths
6. Dockerfile + Helm CronJob + CI image build
7. Local verify with `--run-now` against a seeded dev DB

## Open Questions

- Digest cadence: Friday 9am is the design-doc default — confirm before prod cron. (Code default: Friday.)
- Do renewal alerts respect tier (free vs pro)? MVP: send to all subscribers regardless of tier; gating is a later monetization lever.
- Idempotency key to Resend for crash-window dedup — nice-to-have; the DB guard covers the common case.
