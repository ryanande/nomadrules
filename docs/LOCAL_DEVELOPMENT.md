# Local Development

How to run the NomadRules pipeline on your machine. Production runs in AKS against
Azure Postgres + Key Vault (see `openspec/changes/aks-secure-platform-deployment`);
locally you run one Postgres container and the services as plain processes.

The pipeline: **crawler → (queue) → ingest → `law_changes` → summarizer → email-delivery**,
with the **API** + **portal** serving subscribers. Everything shares one Postgres DB.

## Prerequisites

- .NET 10 SDK (`dotnet --version`)
- Node.js 20+ (crawler + portal)
- Docker (for the Postgres container)
- API keys for the AI/email hops (optional — only the services that use them need them):
  - `ANTHROPIC_API_KEY` — summarizer (Claude)
  - `RESEND_API_KEY` — email-delivery

## 1. Start Postgres

```bash
docker compose -f infra/docker-compose.yml up -d postgres
```

This creates database **`nomadrules_dev`** (user `nomadrules` / password `nomadrules`) on
`localhost:5432` with a persistent volume. The canonical local connection string is:

```
Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules
```

> The API connects to this out of the box in local dev — its launch profile sets
> `ASPNETCORE_ENVIRONMENT=Development` and `appsettings.Development.json` uses `nomadrules_dev`.
> (The base `appsettings.json` default was also aligned to `nomadrules_dev` for consistency; production
> resolves the real connection string from Key Vault regardless.)

## 2. Apply migrations

The DbUp runner applies `Scripts/V*.sql` (Postgres dialect) and exits. Run it before any service:

```bash
cd src/db-migrations/NomadRules.DbMigrations
POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules" \
  ASPNETCORE_ENVIRONMENT=Development dotnet run
```

Re-running is idempotent (already-applied scripts are skipped).

## 3. Run the services

Each is a separate process. Point every one at the same `nomadrules_dev`. The C# services read
`ConnectionStrings:Postgres` from `appsettings.json` / `appsettings.Development.json` (all default to
`nomadrules_dev` for local dev), or `ConnectionStrings__Postgres` / `POSTGRES_CONNECTION_STRING` from the env.

Export once for the shell:

```bash
export CS="Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules"
export DOTNET_ENVIRONMENT=Development
```

**API** (`http://localhost:5017`) — anonymous `POST /api/subscribers` works locally; the
authenticated profile/feed endpoints need real Entra config (see step 5). Dummy Entra values let it boot:

```bash
cd src/api/NomadRules.Api
ConnectionStrings__Postgres="$CS" \
  Entra__Authority="https://example.com" Entra__ClientId="dev" Entra__TenantId="dev" \
  dotnet run
```

**Summarizer** — reads unprocessed `law_changes`, summarizes via Claude, writes back:

```bash
cd src/summarizer/NomadRules.Summarizer
ConnectionStrings__Postgres="$CS" ANTHROPIC_API_KEY="sk-ant-..." dotnet run
```

**Email-delivery** — sends renewal alerts + digests. Fails fast without a Resend key.
`--run-now` does a single pass (handy for testing); a bare run polls on a schedule:

```bash
cd src/email-service/NomadRules.EmailDelivery
ConnectionStrings__Postgres="$CS" RESEND_API_KEY="re_..." dotnet run -- --run-now
```

**Ingest** — drains the crawler's queue into `law_changes`. Locally it reads the file queue
the crawler writes (`TRANSPORT=local`). Point `Ingest:LocalQueueDir` at the crawler's `local-queue`:

```bash
cd src/ingest/NomadRules.Ingest
ConnectionStrings__Postgres="$CS" Ingest__Transport=local \
  Ingest__LocalQueueDir="../../crawler/local-queue" dotnet run -- --run-now
```

Every C# service also supports `dotnet run -- --selfcheck` (pure-logic checks, no DB) as a quick sanity gate.

## 4. Crawler → ingest (the local queue)

The crawler publishes `LawChangeDetected` messages. In prod that's Azure Service Bus; locally it
writes one JSON file per change to `./local-queue/` (`TRANSPORT=local`), which ingest drains.

```bash
cd src/crawler
cp .env.example .env          # TRANSPORT=local, LOCAL_QUEUE_DIR=./local-queue
npm install
npm run build && npm start    # scrapes sources, writes ./local-queue/*.json
```

Then run **ingest** (step 3) pointed at `src/crawler/local-queue` to move those into `law_changes`,
where the summarizer picks them up. To skip live scraping, drop a hand-written
`LawChangeDetected` JSON into `local-queue/` (see `src/crawler/src/types/messages.ts` for the shape).

## 5. Portal

```bash
cd src/portal
cp .env.example .env           # VITE_API_URL=http://localhost:5017, plus Entra values
npm install
npm run dev                    # http://localhost:5173
```

The portal authenticates subscribers via **Entra External ID (CIAM)**. The profile/feed pages call
authenticated API endpoints, so a fully working portal needs real Entra config in both `.env`
(`VITE_ENTRA_*`) and the API (`Entra__*`) — see `openspec/changes/archive/*azure-entra-auth-iac`.
Without it, the API boots with dummy values and only the anonymous register endpoint works.

## Exercising the whole pipeline

1. `docker compose up -d postgres` → run migrations (steps 1–2).
2. Seed a subscriber: `curl -X POST http://localhost:5017/api/subscribers -H 'Content-Type: application/json' -d '{"email":"you@example.com","state":"TX","insuranceRenewalMonth":9,"insuranceRenewalDay":20}'`
3. Produce a change: run the crawler (step 4), or drop a `LawChangeDetected` file into `local-queue/`.
4. `ingest --run-now` → the raw row lands in `law_changes`.
5. `summarizer` → fills in headline/summary/severity (needs `ANTHROPIC_API_KEY`).
6. `email-delivery --run-now` → sends the alert/digest (needs `RESEND_API_KEY`).

## Notes / known issues

- **Secrets are env vars locally, Key Vault in AKS.** Never commit real keys; the compose file and
  appsettings carry only local placeholders.
- **Duplicate `V003` migration scripts** (`V003__entra_oid.sql` + `V003__law_change_dedup.sql`) —
  landed from two parallel changes. DbUp keys on the full script name so both apply, but the shared
  version number is a smell worth renumbering.
- Messaging is Azure Service Bus (prod) / local-file queue (dev) — **not** RabbitMQ, despite older
  references. There is no local broker to run.
