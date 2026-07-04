## 1. New Project Scaffold

- [x] 1.1 Create `src/db-migrations/NomadRules.DbMigrations/` console project targeting net10.0
- [x] 1.2 Add NuGet packages: `DbUp-SQLite`, `Serilog`, `Serilog.Sinks.Console`, `Microsoft.Extensions.Configuration.Json`, `Microsoft.Extensions.Configuration.EnvironmentVariables`
- [x] 1.3 Add observability packages: `prometheus-net.Pushgateway`, `OpenTelemetry`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Serilog.Enrichers.Span` (for trace-id in logs)
- [x] 1.4 Add `src/db-migrations/.gitignore` (bin/, obj/, *.db)

## 2. SQL Migration Scripts

- [x] 2.1 Create `Scripts/V001__init_schema.sql` with content from `src/api/NomadRules.Api/Infrastructure/schema.sql`
- [x] 2.2 Configure `.csproj` to embed all `Scripts/*.sql` as assembly resources (`<EmbeddedResource Include="Scripts\*.sql" />`)

## 3. Program.cs — Migration Runner

- [x] 3.1 Wire `IConfiguration` from `appsettings.json` + env vars
- [x] 3.2 Resolve connection string: `SQLITE_CONNECTION_STRING` env var → `ConnectionStrings:Sqlite` → fatal exit if neither present
- [x] 3.3 Configure Serilog: JSON sink in production, AnsiConsole sink in Development
- [x] 3.4 Build DbUp upgrader: `DeployChanges.To.SQLiteDatabase(connStr).WithScriptsEmbeddedInAssembly(...).LogToSerilog(...).Build()`
- [x] 3.5 Wrap upgrade in a `migration.run` root span; emit a child span per applied script
- [x] 3.6 Run `upgrader.PerformUpgrade()` — log success at INFO, log errors at ERROR, exit 1 on failure

## 4. Observability

- [x] 4.1 Configure Serilog enrichment with trace id / span id (`Enrich.WithSpan()`)
- [x] 4.2 Configure OpenTelemetry tracer with OTLP exporter; disable cleanly if `OTEL_EXPORTER_OTLP_ENDPOINT` unset
- [x] 4.3 Define Prometheus metrics: `migration_scripts_applied_total{result}`, `migration_run_duration_seconds`, `migration_run_timestamp_seconds`
- [x] 4.4 Push metrics to Pushgateway (`PUSHGATEWAY_URL`) keyed by job `db_migration_runner` + instance; disable cleanly if unset
- [x] 4.5 Wrap all telemetry (push + export) in try/catch — log WARN on failure, never affect exit code

## 5. Configuration Files

- [x] 5.1 Create `appsettings.json` with `ConnectionStrings:Sqlite` placeholder
- [x] 5.2 Create `appsettings.Development.json` with local dev SQLite path

## 6. Docker Packaging

- [x] 6.1 Create multi-stage `Dockerfile` — `sdk` build/publish stage, `mcr.microsoft.com/dotnet/runtime` final stage
- [x] 6.2 Confirm embedded scripts are present in the published image (no filesystem/volume dependency)
- [x] 6.3 Build image locally and run against a throwaway SQLite volume to verify exit 0
- [x] 6.4 Add `.dockerignore` (bin/, obj/, *.db)

## 7. API Cleanup

- [x] 7.1 Remove `InitializeAsync()` method from `src/api/NomadRules.Api/Infrastructure/Db.cs`
- [x] 7.2 Remove `await db.InitializeAsync()` call from `src/api/NomadRules.Api/Program.cs`
- [x] 7.3 Delete `src/api/NomadRules.Api/Infrastructure/schema.sql`
- [x] 7.4 Remove `<None Update="Infrastructure\schema.sql">` item from API `.csproj`

## 8. Deployment & CI

- [x] 8.1 Add init container to `infra/helm/` API deployment referencing the migration image
- [x] 8.2 Wire `SQLITE_CONNECTION_STRING`, `PUSHGATEWAY_URL`, `OTEL_EXPORTER_OTLP_ENDPOINT` env vars in Helm values
- [x] 8.3 Add CI step to build + push migration image tagged with commit SHA

## 9. Verification

- [x] 9.1 `dotnet run --project src/db-migrations/NomadRules.DbMigrations` creates DB and exits 0
- [x] 9.2 Second run exits 0 with "no new migrations" log
- [x] 9.3 API starts cleanly after migration runner has run (no schema errors)
- [x] 9.4 Confirm `SchemaVersions` table exists with `V001__init_schema.sql` row
- [ ] 9.5 With telemetry endpoints set, confirm metrics land in Pushgateway and a trace appears in Jaeger _(deferred: needs live Pushgateway + Jaeger; unset-endpoint path verified in 9.6)_
- [x] 9.6 With telemetry endpoints unset/unreachable, confirm migration still exits 0
