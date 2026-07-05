## 1. Migration

- [x] 1.1 Add `Scripts/V003__law_change_dedup.sql` to `NomadRules.DbMigrations` (nullable `source_message_id TEXT` + `UNIQUE` index on it)
- [x] 1.2 Run migration runner locally; confirm column + index and `SchemaVersions` has V003

## 2. Project Scaffold

- [x] 2.1 Create `src/ingest/NomadRules.Ingest/` worker (net10.0), copy summarizer's Generic Host + `Db` + `Migrations` + Serilog + `--selfcheck` wiring
- [x] 2.2 Add packages: `Dapper`, `Microsoft.Data.Sqlite`, `SQLitePCLRaw.bundle_e_sqlite3`, `Serilog`(+console/compact/span), `Azure.Messaging.ServiceBus`
- [x] 2.3 `.gitignore` (bin/, obj/, *.db) + `appsettings.json` / `appsettings.Development.json`
- [x] 2.4 Bind options: `Ingest` (Transport, LocalQueueDir, ServiceBus connection/queue, MvpDefaultState)

## 3. Message Contract + Mapping (pure, self-checkable)

- [x] 3.1 Model `LawChangeDetected` mirroring `src/crawler/src/types/messages.ts` (messageId, sourceId, rawContent, contentHash, previousHash, url, state?, category, detectedAt)
- [x] 3.2 Map message → `law_changes` row: new UUID `id`, source_message_id = messageId, state defaulting to MVP state when absent, processed_at NULL
- [x] 3.3 `--selfcheck`: assert field mapping and that a missing `state` defaults correctly and `id != messageId`

## 4. Transport Sources

- [x] 4.1 `IMessageSource` abstraction (receive-one/batch + ack + set-aside), selected by `Transport` env — mirrors crawler `createPublisher()`
- [x] 4.2 `LocalFileSource`: read `*.json` from `LocalQueueDir`, delete file only after commit
- [x] 4.3 `ServiceBusSource`: `Azure.Messaging.ServiceBus` receiver, complete message only after commit; dead-letter on malformed

## 5. Idempotent Ingest

- [x] 5.1 Repository: `INSERT OR IGNORE INTO law_changes (... source_message_id)` keyed on the unique index
- [x] 5.2 Consume loop: parse → insert → ack-after-commit; duplicate (ignored insert) still acks
- [x] 5.3 Malformed message: log ERROR, set aside (skip file / dead-letter), continue — never crash the worker

## 6. Worker

- [x] 6.1 HostedService draining continuously, plus `--run-now` single drain for local testing / CronJob
- [x] 6.2 Fail fast at startup on missing transport config (e.g. Service Bus selected but no connection string)

## 7. Observability

- [x] 7.1 Serilog JSON (prod) / console (dev), trace-enriched — reuse the established config
- [x] 7.2 Metrics: `law_changes_ingested_total{result}`; best-effort telemetry never blocks an insert

## 8. Docker + Deployment

- [x] 8.1 Multi-stage Dockerfile (runtime base)
- [x] 8.2 Helm: Deployment (prod, Service Bus) + values for transport/connection; local uses `--run-now` against the file queue
- [x] 8.3 CI: build + push image tagged with commit SHA

## 9. Verification

- [x] 9.1 `--selfcheck` passes
- [x] 9.2 Hand-drop a crawler-shaped queue file; `--run-now` inserts one `law_changes` row with `processed_at NULL`
- [x] 9.3 Re-run with the same file/messageId; no second row (idempotent)
- [x] 9.4 Malformed JSON file → logged, set aside, worker continues; valid siblings still ingested
- [x] 9.5 Full local chain: crawler (or dropped file) → ingest → summarizer-shaped row present → (with a seeded summary) email-delivery sends — the end-to-end path the smoke found broken
