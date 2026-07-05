## Why

The pipeline is severed at its very first hop. The crawler detects a change and publishes a `LawChangeDetected` message to a queue (`src/crawler/src/core/messageBus.ts` — local-file in dev, Azure Service Bus in prod), but **nothing consumes it**: an end-to-end smoke found zero code paths that `INSERT` into the `law_changes` table anywhere in the repo. The summarizer polls `law_changes WHERE processed_at IS NULL`, so with no ingest it has nothing to process, and no alert can ever reach a subscriber. Everything downstream (summarizer → email-delivery, verified working) is dead code until raw rows land in `law_changes`.

This closes that gap: a consumer that drains the crawler's queue into `law_changes`, idempotently, so the rest of the pipeline runs.

## What Changes

- New **ingest consumer** that reads `LawChangeDetected` messages and inserts a raw `law_changes` row (`source_id`, `url`, `raw_content`, `state`, `detected_at`, `processed_at = NULL`) for the summarizer to pick up.
- **Idempotent** on message re-delivery: a re-delivered or duplicate message never creates a second row (dedup on the message's identity + content hash).
- Reads the **same transport abstraction the crawler writes** — local-file queue in dev, Azure Service Bus in prod — mirroring the crawler's `createPublisher()` selection so dev needs no cloud.
- Non-changing content is skipped upstream by the crawler's diff engine; ingest only ever sees real changes.
- Insurance-only, TX-only for MVP (matches crawler + summarizer scope); `category` on the message is carried but not yet used for routing.

Out of scope (flagged, not resolved here): the crawler publishes to **Azure Service Bus** while `CLAUDE.md` still names AsyncBus as RabbitMQ/AMQP. This change follows the crawler's actual transport; reconciling the documented messaging stack is a separate decision.

## Capabilities

### New Capabilities

- `law-change-ingest`: Consumes `LawChangeDetected` messages from the crawler's queue and persists them as raw `law_changes` rows for summarization, exactly once per detected change, across both the local-file (dev) and Azure Service Bus (prod) transports.

### Modified Capabilities

<!-- No existing spec's requirements change. This adds the missing consumer; law_changes schema is already defined (V001). -->

## Impact

- New consumer (host + transport reader). Placement — a dedicated ingest worker vs. folding the consumer into the existing summarizer — is the central design decision (see design.md).
- Reads the crawler's message contract (`src/crawler/src/types/messages.ts`): `messageId`, `sourceId`, `rawContent`, `contentHash`, `previousHash`, `url`, `state?`, `category`, `detectedAt`.
- Writes existing `law_changes` (V001 schema); may add a nullable `content_hash` / `source_message_id` column for dedup (additive migration).
- Local dev: reads `LOCAL_QUEUE_DIR` files the crawler already writes; prod: an Azure Service Bus receiver on the crawler's queue.
- Unblocks the summarizer → email-delivery path end to end.
