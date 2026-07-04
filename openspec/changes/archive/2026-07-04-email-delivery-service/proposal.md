## Why

The renewal-alert is the product wedge — the ROI moment subscribers pay for ("protect your insurance renewal"). Everything upstream (crawler → summarizer) produces data, but nothing yet turns it into the emails that deliver value. This service closes the loop: renewal-timed alerts and a weekly law-change digest, matched to each subscriber's state and renewal calendar.

## What Changes

- New `NomadRules.EmailDelivery` C# worker service under `src/email-service/`
- Sends **renewal alerts** at 60/30/7 days before each subscriber's insurance/registration/license/tax renewal
- Sends a **weekly digest** of processed law changes matched by state (+ renewal relevance)
- **Urgent-severity** law changes bypass the digest as immediate alerts
- Resend API integration (direct HTTP, matching the summarizer's direct-Claude pattern)
- New migration `V002__renewal_alerts.sql` — renewal alerts aren't tied to a `law_change`, so they get their own idempotency table
- Follows the established worker patterns: Generic Host + polling worker, `Db`/repository, `Migrations.Apply` fail-fast on startup, Serilog + observability, `--selfcheck` runnable check

## Capabilities

### New Capabilities

- `email-delivery`: Event-driven delivery worker — computes due renewal alerts and weekly digests from the SQLite schema, renders + sends via Resend, records sends idempotently so no subscriber is double-emailed.

### Modified Capabilities

<!-- No existing spec's requirements change. The schema addition (V002) is additive and covered under email-delivery. -->

## Impact

- New project: `src/email-service/NomadRules.EmailDelivery/`
- New migration: `src/db-migrations/NomadRules.DbMigrations/Scripts/V002__renewal_alerts.sql` (adds `renewal_alerts` table)
- New dependency: Resend API key (`RESEND_API_KEY`) — already listed in CLAUDE.md critical links
- Reads existing tables: `subscribers`, `law_changes`, `notifications`
- `infra/helm/` + `.github/workflows/` — new deployment (CronJob) + image build
- Known simplification: renewal dates are stored as a **month** (1-12), not a full date. MVP anchors the renewal to the 1st of that month for day-offset triggers; full-date entry is a follow-up (see design).
