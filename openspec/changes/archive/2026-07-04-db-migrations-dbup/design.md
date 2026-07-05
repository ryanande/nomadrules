## Context

The API currently owns schema creation via `Db.InitializeAsync()`, which executes `schema.sql` as raw DDL on every startup. This is fine for a single instance booting fresh but breaks in two real scenarios: multiple API pods racing to apply the same DDL, and iterative schema changes (ALTER TABLE, index additions) that need to run exactly once. DbUp solves this with a `SchemaVersions` journal table that tracks which scripts have been applied.

Stack: C# 10+, SQLite (v0.1), Dapper in the API (no EF Core). DbUp is SQL-script-based, not code-first — a natural fit.

## Goals / Non-Goals

**Goals:**
- Migration runner runs as a standalone process (K8s init container or `dotnet run`) before the API starts
- Scripts apply in deterministic order, exactly once, using DbUp's journal table
- Structured logging via Serilog (JSON in production, pretty-print in dev)
- Fail fast: non-zero exit on any migration failure so K8s restarts the init container rather than starting the API against a broken schema
- Connection string from env var (`SQLITE_CONNECTION_STRING`) with fallback to `appsettings.json`
- Local dev: `dotnet run --project src/db-migrations/NomadRules.DbMigrations` just works

- Migration runner ships as a self-contained Docker image, built and pushed in CI
- Observability is first-class: every run emits structured logs, push-based metrics, and a trace, visible in the same Prometheus/Grafana/Jaeger stack as the services

**Non-Goals:**
- Rollback / down migrations (DbUp doesn't support them; broken migrations get a new fix-forward script)
- EF Core integration
- Multi-database support (SQLite only for v0.1)
- Migration UI or web endpoint
- Long-running metrics server inside the runner (it's ephemeral — push, don't expose a scrape endpoint)

## Decisions

### Decision 1: DbUp over Evolve / EF Core Migrations

**Chosen:** DbUp

**Alternatives:**
- **Evolve**: Flyway-style filename convention, less .NET-native, smaller community
- **EF Core Migrations**: Code-first, excellent tooling, but requires EF Core across the project — we're on Dapper

**Rationale:** DbUp is SQL-first, battle-tested in .NET, integrates with any connection type including SQLite. The team has prior familiarity. Script-based migrations are readable, diffable, and reviewable as plain SQL.

---

### Decision 2: Separate console project, not embedded in API

**Chosen:** `NomadRules.DbMigrations` console project

**Alternatives:**
- Embed migration logic in `NomadRules.Api` startup — simpler, one fewer project
- Run migrations as a hosted service inside the API

**Rationale:** Separation of concerns: the API should not own schema management. Init container pattern is idiomatic K8s — the migration container starts, runs to completion (exit 0), then the API container starts. This also makes it easy to run migrations in CI before integration tests without standing up the full API.

---

### Decision 3: Serilog with console + file sinks

**Chosen:** Serilog with `WriteTo.Console(JsonFormatter)` in production, `WriteTo.Console(theme: AnsiConsoleTheme.Code)` in dev

**Alternatives:**
- `Microsoft.Extensions.Logging` only — less structured, harder to correlate with API logs
- Application Insights sink — adds Azure dependency; overkill for a migration runner

**Rationale:** Structured JSON logs from the migration runner feed into the same log aggregator (ELK/Seq) as the API. Dev gets readable colored output. Single NuGet dependency (`Serilog.Sinks.Console`).

---

### Decision 4: Script naming convention `V{NNN}__{description}.sql`

**Chosen:** `V001__init_schema.sql`, `V002__add_index.sql`, etc.

**Rationale:** Three-digit zero-padded prefix sorts correctly up to 999 migrations (enough for years). Double-underscore separator is DbUp convention. Human-readable description aids git blame. DbUp discovers scripts embedded as assembly resources — no filesystem dependency in the container.

---

### Decision 5: Connection string precedence

**Chosen:** `SQLITE_CONNECTION_STRING` env var → `ConnectionStrings:Sqlite` in `appsettings.json`

**Rationale:** Env var override is the K8s-native pattern (secret injected as env var). `appsettings.json` fallback makes local dev work without any env setup.

---

### Decision 6: Self-contained Docker image, multi-stage build

**Chosen:** Multi-stage Dockerfile — `sdk` image builds + publishes, `runtime-deps` (or `aspnet`-free `runtime`) image runs. Scripts are embedded resources, so no volume mounts.

**Alternatives:**
- Bake migrations into the API image and run via entrypoint flag — couples runner lifecycle to API image, defeats the init-container separation
- Mount scripts from a ConfigMap — fragile, size-limited (1MB ConfigMap cap), and scripts drift from the binary that runs them

**Rationale:** A dedicated image is the unit the init container references. Because scripts are compiled in as embedded resources, the image is hermetic — the exact scripts that shipped are the exact scripts that run. Image is tagged with the same SHA as the API so a deploy is one coherent version. Use `mcr.microsoft.com/dotnet/runtime` (not `aspnet`) — no web server needed.

---

### Decision 7: Push-based observability for an ephemeral job

**Chosen:** Serilog (logs) + Prometheus **Pushgateway** (metrics) + OpenTelemetry OTLP exporter (one trace per run)

**Alternatives:**
- Expose a `/metrics` endpoint and let Prometheus scrape — **wrong for batch jobs**: the process exits in seconds, long before the next scrape interval, so the data is never collected
- Logs only — loses queryable metrics (how long did migrations take? how many applied? failure rate over time?)
- Full OTel metrics pipeline — heavier; Pushgateway is the idiomatic Prometheus answer for short-lived jobs

**Rationale:** The defining constraint is that this process is **ephemeral**. Prometheus's pull model can't observe something that's already gone, so metrics are *pushed* to a Pushgateway keyed by job name (`db_migration_runner`) and instance (pod name / run id). Metrics emitted:
- `migration_scripts_applied_total{result}` — counter
- `migration_run_duration_seconds` — gauge (final value pushed at end)
- `migration_run_timestamp_seconds` — gauge (last-success tracking for alerting on staleness)

Traces: a single root span `migration.run` with a child span per applied script, exported via OTLP to the collector → Jaeger. This is arguably generous for a migration job, but the user wants observability as a first-class citizen, and one root span is cheap. Trace + logs share the same correlation id so a failed migration in Grafana links straight to its trace in Jaeger.

**Trade-off:** Pushgateway is a small extra piece of infra. It's a standard component already implied by the observability stack in `CLAUDE.md`. If the team decides it's not worth running, metrics degrade gracefully — the runner logs a warning and continues (telemetry failure must never fail a migration).

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Migration script has a bug; DB is partially applied | DbUp wraps each script in a transaction; partial apply rolls back. Add a fix-forward `V00N__fix_*.sql`. |
| Init container and API race in multi-pod deploy | K8s init container guarantee: API container does not start until init container exits 0. No race. |
| SQLite file not present when migration runner starts | Runner creates the DB file on first connect (SQLite behavior). Safe. |
| Devs forget to add new scripts to the project as embedded resources | `.csproj` glob `<EmbeddedResource Include="Scripts\*.sql" />` picks up all scripts automatically. |
| Pushgateway / OTLP collector unreachable at runtime | Telemetry is best-effort: wrap push + export in try/catch, log a warning, never fail the migration. A schema migration must not depend on the observability stack being up. |
| Stale "last successful migration" goes unnoticed | `migration_run_timestamp_seconds` pushed to Pushgateway enables a Prometheus alert if no successful run in N days. |

## Migration Plan

1. Create `NomadRules.DbMigrations` project with DbUp + Serilog + telemetry
2. Move `schema.sql` content into `Scripts/V001__init_schema.sql`
3. Add Dockerfile (multi-stage, runtime base)
4. Remove `Db.InitializeAsync()` from API
5. Update `infra/helm/` to add init container + Pushgateway/OTLP env vars
6. Update CI to build + push migration image (tagged with commit SHA)
7. On deploy: init container runs migrations, pushes metrics/trace, API starts after

## Open Questions

- Do we need a `--dry-run` flag that logs pending scripts without applying? Useful for CI validation. Add in a follow-up if needed.
- When we migrate from SQLite to PostgreSQL (post-MVP), the script content changes but the runner pattern stays identical — DbUp supports both.
