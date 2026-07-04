## ADDED Requirements

### Requirement: Push-based metrics for migration runs
The system SHALL push run metrics to a Prometheus Pushgateway because the runner is ephemeral and exits before a scrape interval can collect them.

#### Scenario: Successful run pushes metrics
- **WHEN** a migration run completes successfully
- **THEN** the runner pushes `migration_scripts_applied_total{result="success"}`, `migration_run_duration_seconds`, and `migration_run_timestamp_seconds` to the Pushgateway
- **AND** metrics are keyed by job `db_migration_runner` and an instance label (pod name or run id)

#### Scenario: Failed run pushes failure metric
- **WHEN** a migration run fails
- **THEN** the runner pushes `migration_scripts_applied_total{result="failure"}` before exiting non-zero

#### Scenario: Pushgateway unreachable
- **WHEN** the Pushgateway endpoint is unreachable
- **THEN** the runner logs a warning at WARN level
- **AND** the migration outcome (exit code) is unaffected — telemetry failure never fails a migration

### Requirement: Distributed trace per migration run
The system SHALL emit one OpenTelemetry trace per run, exported via OTLP, with a root span covering the run and a child span per applied script.

#### Scenario: Trace emitted on run
- **WHEN** a migration run executes
- **THEN** a root span `migration.run` is created
- **AND** each applied script produces a child span tagged with the script name and duration
- **AND** spans are exported to the configured OTLP collector endpoint

#### Scenario: Trace correlation with logs
- **WHEN** the runner logs during a run
- **THEN** log entries include the trace id and span id so logs in Grafana link to the trace in Jaeger

#### Scenario: OTLP collector unreachable
- **WHEN** the OTLP collector endpoint is unreachable
- **THEN** the runner logs a warning and continues — trace export failure never fails a migration

### Requirement: Telemetry endpoints from configuration
The system SHALL resolve the Pushgateway and OTLP collector endpoints from environment variables, and disable the corresponding exporter cleanly if its endpoint is unset.

#### Scenario: Endpoints configured
- **WHEN** `PUSHGATEWAY_URL` and `OTEL_EXPORTER_OTLP_ENDPOINT` are set
- **THEN** the runner pushes metrics and exports traces to those endpoints

#### Scenario: Endpoint unset
- **WHEN** an endpoint env var is not set
- **THEN** the corresponding exporter is disabled
- **AND** the runner logs at INFO that the exporter is disabled
- **AND** the run proceeds normally
