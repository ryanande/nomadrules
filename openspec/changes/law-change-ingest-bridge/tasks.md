## 1. Migration

- [ ] 1.1 Add `Scripts/V003__law_change_dedup.sql` to `NomadRules.DbMigrations` (nullable `source_message_id TEXT` + `UNIQUE` index on it)
- [ ] 1.2 Run migration runner locally; confirm column + index and `SchemaVersions` has V003

## 2. Project Scaffold

- [ ] 2.1 Create `src/ingest/NomadRules.Ingest/` worker (net10.0), copy summarizer's Generic Host + `Db` + `Migrations` + Serilog + `--selfcheck` wiring
- [ ] 2.2 Add packages: `Dapper`, `Microsoft.Data.Sqlite`, `SQLitePCLRaw.bundle_e_sqlite3`, `Serilog`(+console/compact/span), `Azure.Messaging.ServiceBus`
- [ ] 2.3 `.gitignore` (bin/, obj/, *.db) + `appsettings.json` / `appsettings.Development.json`
- [ ] 2.4 Bind options: `Ingest` (Transport, LocalQueueDir, ServiceBus connection/queue, MvpDefaultState)

## 3. Message Contract + Mapping (pure, self-checkable)

- [ ] 3.1 Model `LawChangeDetected` mirroring `src/crawler/src/types/messages.ts` (messageId, sourceId, rawContent, contentHash, previousHash, url, state?, category, detectedAt)
- [ ] 3.2 Map message → `law_changes` row: new UUID `id`, source_message_id = messageId, state defaulting to MVP state when absent, processed_at NULL
- [ ] 3.3 `--selfcheck`: assert field mapping and that a missing `state` defaults correctly and `id != messageId`

## 4. Transport Sources

- [ ] 4.1 `IMessageSource` abstraction (receive-one/batch + ack + set-aside), selected by `Transport` env — mirrors crawler `createPublisher()`
- [ ] 4.2 `LocalFileSource`: read `*.json` from `LocalQueueDir`, delete file only after commit
- [ ] 4.3 `ServiceBusSource`: `Azure.Messaging.ServiceBus` receiver, complete message only after commit; dead-letter on malformed

## 5. Idempotent Ingest

- [ ] 5.1 Repository: `INSERT OR IGNORE INTO law_changes (... source_message_id)` keyed on the unique index
- [ ] 5.2 Consume loop: parse → insert → ack-after-commit; duplicate (ignored insert) still acks
- [ ] 5.3 Malformed message: log ERROR, set aside (skip file / dead-letter), continue — never crash the worker

## 6. Worker

- [ ] 6.1 HostedService draining continuously, plus `--run-now` single drain for local testing / CronJob
- [ ] 6.2 Fail fast at startup on missing transport config (e.g. Service Bus selected but no connection string)

## 7. Observability

- [ ] 7.1 Serilog JSON (prod) / console (dev), trace-enriched — reuse the established config
- [ ] 7.2 Metrics: `law_changes_ingested_total{result}`; best-effort telemetry never blocks an insert

## 8. Docker + Deployment

- [ ] 8.1 Multi-stage Dockerfile (runtime base)
- [ ] 8.2 Helm: Deployment (prod, Service Bus) + values for transport/connection; local uses `--run-now` against the file queue
- [ ] 8.3 CI: build + push image tagged with commit SHA

## 9. Verification

- [ ] 9.1 `--selfcheck` passes
- [ ] 9.2 Hand-drop a crawler-shaped queue file; `--run-now` inserts one `law_changes` row with `processed_at NULL`
- [ ] 9.3 Re-run with the same file/messageId; no second row (idempotent)
- [ ] 9.4 Malformed JSON file → logged, set aside, worker continues; valid siblings still ingested
- [ ] 9.5 Full local chain: crawler (or dropped file) → ingest → summarizer-shaped row present → (with a seeded summary) email-delivery sends — the end-to-end path the smoke found broken
