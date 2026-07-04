## Why

The API currently initializes the database by executing a raw `schema.sql` file on startup via `InitializeAsync()`, with no version tracking, no rollback, and no separation of concerns. As the schema evolves we need auditable, ordered migrations that run once and never repeat — especially critical before K8s init containers and multi-instance deploys.

## What Changes

- New `NomadRules.DbMigrations` C# console project under `src/db-migrations/`
- DbUp wiring with Serilog structured logging (replaces console output)
- Versioned SQL scripts (`V001__init.sql`, `V002__...`) replace the monolithic `schema.sql`
- `Db.InitializeAsync()` removed from the API; API no longer owns schema management
- Migration runner executes as a K8s init container before the API pod starts, or standalone CLI for local dev
- Connection string sourced from environment variable or `appsettings.json`
- Runner packaged as a self-contained Docker image, built and pushed in CI
- Observability as a first-class concern: structured logs (Serilog), push-based metrics (Prometheus Pushgateway), and distributed traces (OpenTelemetry/OTLP) — designed for an ephemeral batch job that exits before a scrape can occur

## Capabilities

### New Capabilities

- `db-migration-runner`: Standalone migration runner — discovers and applies versioned SQL scripts in order, logs each applied migration, fails fast on error so the API pod never starts against a bad schema.
- `migration-observability`: Telemetry for the migration runner — structured logs, push-based metrics (count applied, duration, outcome), and a trace per run so migrations are visible in the same observability stack as the services.

### Modified Capabilities

- `subscriber-api`: Remove `Db.InitializeAsync()` call and schema ownership from `Program.cs`; API trusts the schema is already applied.

## Impact

- `src/api/NomadRules.Api/Infrastructure/Db.cs` — remove `InitializeAsync`
- `src/api/NomadRules.Api/Program.cs` — remove `db.InitializeAsync()` call
- `src/api/NomadRules.Api/Infrastructure/schema.sql` — replaced by versioned scripts; file deleted
- New project: `src/db-migrations/NomadRules.DbMigrations/`
- New `Dockerfile` for the migration runner image
- `infra/helm/` — new init container spec referencing the migration image; Pushgateway + OTLP collector endpoints wired as env vars
- `.github/workflows/` — migration image build + push added to CI
- New dependencies: `prometheus-net.Pushgateway`, `OpenTelemetry` + OTLP exporter
