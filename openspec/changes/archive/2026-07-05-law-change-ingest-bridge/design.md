## Context

The crawler (`src/crawler`, TypeScript) scrapes sources, diffs against a stored snapshot, and on a real change publishes a `LawChangeDetected` message via `createPublisher()` — `LocalFilePublisher` writes one JSON file per message under `LOCAL_QUEUE_DIR` (dev), `ServiceBusPublisher` sends to an Azure Service Bus queue (prod). The message carries `messageId`, `sourceId`, `rawContent`, `contentHash`, `previousHash`, `url`, `state?`, `category`, `detectedAt`.

The summarizer (`src/summarizer`, C#) polls `law_changes WHERE raw_content IS NOT NULL AND processed_at IS NULL`, summarizes via Claude, and writes back headline/summary/severity/`processed_at`. Email-delivery then reads processed rows. Both downstream hops are verified working — but the `law_changes` table has **no writer**. This change adds it.

`law_changes` (V001) columns: `id, source_id, url, raw_content, headline, summary, severity, state, detected_at, processed_at`. Ingest fills the left half and leaves the summarizer's columns null.

## Goals / Non-Goals

**Goals:**
- Every `LawChangeDetected` message becomes exactly one `law_changes` row with `processed_at = NULL`.
- Idempotent: re-delivered messages (Service Bus at-least-once) and re-scraped identical content never create duplicate rows.
- One codebase reads both transports, selected by env, mirroring the crawler — dev needs no cloud.
- Reuses the summarizer's proven worker patterns (Generic Host, `Db`, fail-fast migrations, `--selfcheck`).

**Non-Goals:**
- Resolving the RabbitMQ-vs-Azure-Service-Bus documentation drift (flagged in proposal; separate decision).
- Category/state routing beyond storing the fields (insurance + TX only for MVP).
- Replacing the summarizer's DB-poll with direct queue-to-summary (see Decision 1 — deliberately kept as two stages).
- Dead-letter/poison-message handling beyond logging + leaving the message for redelivery (v0.1).

## Decisions

### Decision 1: Dedicated ingest worker, not folded into the summarizer

**Chosen:** A new small C# worker `NomadRules.Ingest` that consumes the queue and inserts raw rows. The summarizer keeps polling `law_changes` unchanged.

**Alternatives:**
- Fold the consumer into the summarizer (summarizer consumes the queue, inserts, then its own poll picks the row up) — one fewer deployable.
- Summarizer consumes and summarizes in one step (no raw row, no poll) — collapses two stages into one.

**Rationale:** Keeping ingest separate preserves the autonomous-services / choreography principle and the summarizer's single responsibility (Claude summarization + retry/quota logic). The DB row is a durable buffer: if Claude is rate-limited for an hour, ingested rows simply wait — decoupled from message-queue retention limits. Ingest is I/O-trivial and independently scalable/restartable. Folding them couples message-consumption lifecycle to Claude availability.

**Trade-off:** One more deployable (worker + Dockerfile + Helm). Justified by the decoupling; the worker is tiny.

### Decision 2: Transport abstraction mirrors the crawler's `createPublisher()`

**Chosen:** An `IMessageSource` with `LocalFileSource` (reads + deletes `LOCAL_QUEUE_DIR/*.json`) and `ServiceBusSource` (Azure Service Bus receiver), selected by `TRANSPORT` env exactly as the crawler selects its publisher.

**Rationale:** Symmetry with the producer means dev runs entirely on the filesystem queue the crawler already writes — no cloud dependency to smoke-test the full crawler → ingest → summarizer → email path locally. Prod flips one env var.

**Trade-off:** Two source implementations to maintain. Small; the crawler already proves the shape.

### Decision 3: Idempotency via a dedup key on `law_changes`

**Chosen:** Add a nullable `source_message_id TEXT` column (the crawler's `messageId`) with a `UNIQUE` index, and insert with `INSERT OR IGNORE`. A re-delivered message hits the existing row and is a no-op. Additive migration `V003__law_change_dedup.sql`.

**Alternatives:**
- Dedup on `content_hash` — collapses genuinely-re-detected identical content, but two distinct sources with identical text would wrongly merge; and a real re-change with the same hash as a prior state is legitimately new.
- Use `messageId` as the primary key `id` — simplest, but couples our row identity to the producer's id and complicates the summarizer/notifications foreign keys already keyed on `id`.

**Rationale:** `messageId` is the message's identity; deduping on it is precisely "process each message once." `INSERT OR IGNORE` on a UNIQUE index is the atomic guard — no read-then-write race. `id` stays an independent UUID we own.

**Trade-off:** If the crawler ever re-emits the same logical change with a fresh `messageId`, we'd ingest twice. Acceptable — the crawler's diff engine suppresses unchanged content upstream, so that requires a genuine re-change.

### Decision 4: Ack only after the row is committed

**Chosen:** Read message → `INSERT OR IGNORE` → on success (or benign ignore) delete the local file / complete the Service Bus message. On DB failure, do **not** ack, so the message is redelivered.

**Rationale:** At-least-once from the queue + idempotent insert = effectively-once in the DB. Crash between commit and ack → redelivery → `INSERT OR IGNORE` no-ops → still one row.

**Trade-off:** A committed-but-un-acked message is reprocessed once; harmless by Decision 3.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Service Bus at-least-once causes dup rows | `UNIQUE(source_message_id)` + `INSERT OR IGNORE` (Decision 3) |
| Local-file queue grows unbounded if ingest is down | Ingest deletes each file after commit; crawler writes are low-volume (insurance/TX) |
| Poison message (malformed JSON) blocks the queue | Log at ERROR, move/skip the single file (dev) or dead-letter (Service Bus); never crash the worker |
| Transport doc drift (RabbitMQ vs Service Bus) | Out of scope; this change follows the crawler's real transport and flags the mismatch |
| Ingested row never summarized (no Claude key) | Not this change's concern — the row waits in `processed_at IS NULL`; surfaced by the e2e smoke separately |

## Migration Plan

1. Add `V003__law_change_dedup.sql` (nullable `source_message_id` + unique index) to `NomadRules.DbMigrations`.
2. Scaffold `NomadRules.Ingest` worker (copy summarizer's Host/`Db`/migrations/Serilog/`--selfcheck`).
3. Implement `IMessageSource` (local-file + Service Bus), selected by `TRANSPORT`.
4. Implement idempotent insert + ack-after-commit.
5. `--selfcheck` for message→row mapping and dedup-key construction; `--run-now` single drain for local testing.
6. Dockerfile + Helm (Deployment, or CronJob draining the local queue in dev).
7. Local verify: run crawler (or hand-drop a queue file) → ingest → confirm a `law_changes` row appears with `processed_at NULL`.
