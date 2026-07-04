## 1. Migration

- [ ] 1.1 Add `Scripts/V002__renewal_alerts.sql` to `NomadRules.DbMigrations` (renewal_alerts table per design Decision 2)
- [ ] 1.2 Run migration runner locally; confirm `renewal_alerts` table + `SchemaVersions` has V002

## 2. Project Scaffold

- [ ] 2.1 Create `src/email-service/NomadRules.EmailDelivery/` worker project (net10.0), copy summarizer's Generic Host + `Db` + `Migrations` + Serilog wiring
- [ ] 2.2 Add packages: `Dapper`, `Microsoft.Data.Sqlite`, `SQLitePCLRaw.bundle_e_sqlite3`, `Serilog`, `Serilog.Sinks.Console`, `Serilog.Formatting.Compact`
- [ ] 2.3 Add `.gitignore` (bin/, obj/, *.db) and `appsettings.json` / `appsettings.Development.json`
- [ ] 2.4 Bind options: `Resend` (ApiKey, FromAddress), `Delivery` (TickInterval, DigestDayOfWeek)

## 3. Trigger + Matching Logic (pure, self-checkable)

- [ ] 3.1 Implement renewal anchor: month → 1st-of-month date, roll to next year if passed
- [ ] 3.2 Implement offset check: is anchored date exactly 60/30/7 days from a given "today"
- [ ] 3.3 Implement digest matching: subscriber state → eligible processed law_changes minus already-sent
- [ ] 3.4 Implement idempotency-key construction for renewal_alerts and notifications
- [ ] 3.5 `--selfcheck`: assert anchor math, offset boundaries, digest exclusion, key shape

## 4. Repository (SQLite reads/writes)

- [ ] 4.1 Read subscribers with renewal months by category
- [ ] 4.2 Reserve renewal_alert row (`INSERT OR IGNORE`), return whether this caller won it
- [ ] 4.3 Read processed law_changes for a state excluding already-sent per subscriber
- [ ] 4.4 Reserve + mark `notifications` rows (digest / urgent) idempotently
- [ ] 4.5 Set `sent_at` after successful send

## 5. Resend Client

- [ ] 5.1 Direct `HttpClient` POST to `https://api.resend.com/emails` with bearer `RESEND_API_KEY`
- [ ] 5.2 Fail fast at startup if no API key configured
- [ ] 5.3 On send failure: log ERROR, leave `sent_at` unset, do not throw out of the tick

## 6. Delivery Worker

- [ ] 6.1 HostedService: tick on `Delivery:TickInterval` (default daily), plus `--run-now` single pass
- [ ] 6.2 Each tick: compute due renewal alerts → reserve → render → send → mark
- [ ] 6.3 Each tick: urgent law changes → reserve → send immediately → mark
- [ ] 6.4 On digest day: assemble per-subscriber digest (skip if empty) → reserve → send → mark
- [ ] 6.5 Plain-text/simple-HTML templates for renewal alert, urgent alert, weekly digest

## 7. Observability

- [ ] 7.1 Serilog JSON (prod) / console (dev), trace-enriched — reuse db-migrations config
- [ ] 7.2 Metrics: emails_sent_total{type,result}, daily send count (Resend quota watch)
- [ ] 7.3 Best-effort telemetry — a dead metrics sink never blocks or fails a send

## 8. Docker + Deployment

- [ ] 8.1 Multi-stage Dockerfile (runtime base)
- [ ] 8.2 Helm: CronJob (daily) invoking `--run-now`, with RESEND_API_KEY + connection string env
- [ ] 8.3 CI: build + push image tagged with commit SHA

## 9. Verification

- [ ] 9.1 `--selfcheck` passes
- [ ] 9.2 Seed a dev subscriber with a renewal month 60 days out; `--run-now` sends one alert, records the row
- [ ] 9.3 Second `--run-now` sends nothing (idempotent)
- [ ] 9.4 Seed processed law_changes for TX; digest-day `--run-now` sends one digest, records notifications
- [ ] 9.5 Urgent-severity change triggers an immediate alert outside the digest day
- [ ] 9.6 Empty digest week sends no email
- [ ] 9.7 Missing RESEND_API_KEY → fatal at startup
