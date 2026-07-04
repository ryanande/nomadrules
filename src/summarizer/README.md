# NomadRules.Summarizer

Background worker that turns raw law-change content into plain-English summaries via the Claude API.

It polls `law_changes` for rows that have `raw_content` but no `processed_at`, calls Claude with the
insurance-specific prompt, and writes back `headline`, `summary`, `severity`, and `processed_at`.

## Run

```bash
export ANTHROPIC_API_KEY=sk-ant-...
cd src/summarizer/NomadRules.Summarizer
dotnet run
```

`dotnet run -- --selfcheck` runs the pure-logic assertions (severity/cost/review-gate) without touching Claude or the DB.

## Config (`appsettings.json` or env)

| Key | Default | Notes |
|-----|---------|-------|
| `ConnectionStrings:Sqlite` | `../../api/.../nomadrules.db` | **Must point at the same SQLite file the API writes.** |
| `Claude:ApiKey` | _(env `ANTHROPIC_API_KEY`)_ | |
| `Claude:Model` | `claude-opus-4-8` | High-volume? Consider `claude-sonnet-4-6`/`claude-haiku-4-5` for cost. |
| `Claude:InputCostPer1M` / `OutputCostPer1M` | `5` / `25` | Per-summary cost is logged to `summarizer_costs`. |
| `Claude:TimeoutSeconds` | `30` | Per-call timeout; a timeout counts as one failed attempt. |
| `Summarizer:MaxRetries` | `2` | 1 initial + 2 retries (exponential backoff), then a fallback headline. |
| `Summarizer:QuotaBackoffMinutes` | `60` | On a 429 the row is deferred this long (no retry burned). |
| `Summarizer:ReviewThreshold` | `10` | First N summaries are held (`reviewed = 0`) for manual approval. |

## Secrets & deployment

`ANTHROPIC_API_KEY` (and the DB connection string) are the only secrets. Where the key belongs by environment:

| Environment | How to supply the key |
|-------------|-----------------------|
| **Local dev / live test** | `export ANTHROPIC_API_KEY=sk-ant-...` — the worker reads it by default. Don't put it in `appsettings.json`. |
| **Production (K8s)** | A **Kubernetes Secret** injected as the `ANTHROPIC_API_KEY` env var into the summarizer Deployment. Source the value from **Key Vault** (external-secrets operator or the CSV/CSI secret-store driver) — do **not** hardcode it in Helm values or commit it. |
| **GitHub Actions** | Not needed — no workflow calls Claude (build + `--selfcheck` don't touch the API). Only add `secrets.ANTHROPIC_API_KEY` if you introduce a workflow that hits the live API, and gate that job to `workflow_dispatch`/scheduled (not every PR) so cost stays bounded. |

The worker fails fast at startup if no key is configured (`FATAL: no Claude API key configured ...`).

> **Deployment TODO:** wire the Key Vault → K8s Secret → Deployment env path (external-secrets) when the summarizer's Helm chart is added under `infra/helm/`.

## Schema

The worker owns additive columns on `law_changes` (`retry_count`, `reviewed`, `hallucination`,
`next_attempt_at`) plus the `summarizer_costs` table. They are applied idempotently on startup.
When DbUp migrations land (`openspec/changes/db-migrations-dbup`), fold these in there.

## v0.1 deviations from the spec

- **Review gate doesn't stall the pipeline.** First 10 summaries are held (`reviewed = 0`); #11+ auto-set
  `reviewed = 1`. Approval = flipping `reviewed = 1` on the held rows (a manual/API action); the email
  service filters on `reviewed = 1`. Blocking the worker on human approval would contradict the
  "self-managing pipeline" goal.
- **Alerts are logs, not emails.** "First 10 ready for review" and "Claude quota exceeded" are structured
  log warnings. Actual notification is the email-delivery service's bounded context.
- **Hallucination is a data-model seam.** The `hallucination` column is set during manual review
  (the spec scenario is "Jenn marks as hallucination"); the worker does not auto-detect.
- **Prompt path** is `Prompts/SummarizeInsuranceChange.txt` here (spec said `src/processor/...`; the repo
  uses `src/summarizer`).
