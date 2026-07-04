## ADDED Requirements

### Requirement: Migration runner applies pending scripts in order
The system SHALL discover all embedded SQL scripts matching `V{NNN}__*.sql`, apply any not already recorded in the DbUp journal, and exit 0 on success.

#### Scenario: First run on empty database
- **WHEN** the migration runner starts against a database with no `SchemaVersions` table
- **THEN** all scripts are applied in ascending version order
- **AND** each applied script is logged at INFO level with its name and duration
- **AND** the process exits with code 0

#### Scenario: Subsequent run with no new scripts
- **WHEN** the migration runner starts and all scripts are already in `SchemaVersions`
- **THEN** no scripts are applied
- **AND** runner logs "No new migrations to apply" at INFO level
- **AND** the process exits with code 0

#### Scenario: New script applied on upgrade
- **WHEN** a new `V002__*.sql` script exists and only `V001__*.sql` is in `SchemaVersions`
- **THEN** only `V002__*.sql` is applied
- **AND** `SchemaVersions` is updated with the new entry
- **AND** the process exits with code 0

#### Scenario: Script fails
- **WHEN** a SQL script contains invalid SQL or a constraint violation
- **THEN** the runner logs the error at ERROR level with script name and exception detail
- **AND** the process exits with code 1

### Requirement: Connection string from environment or config
The system SHALL resolve the SQLite connection string from `SQLITE_CONNECTION_STRING` env var first, falling back to `ConnectionStrings:Sqlite` in `appsettings.json`.

#### Scenario: Environment variable present
- **WHEN** `SQLITE_CONNECTION_STRING` is set in the environment
- **THEN** the runner uses that value as the connection string

#### Scenario: Environment variable absent, appsettings present
- **WHEN** `SQLITE_CONNECTION_STRING` is not set
- **AND** `ConnectionStrings:Sqlite` is present in `appsettings.json`
- **THEN** the runner uses the appsettings value

#### Scenario: Neither source configured
- **WHEN** neither the env var nor appsettings connection string is present
- **THEN** the runner logs a fatal error and exits with code 1

### Requirement: Structured logging via Serilog
The system SHALL emit structured logs — JSON format in production, human-readable colored format in development.

#### Scenario: Production logging
- **WHEN** `ASPNETCORE_ENVIRONMENT` is not `Development`
- **THEN** logs are emitted as JSON to stdout (compatible with ELK/Seq ingestion)

#### Scenario: Development logging
- **WHEN** `ASPNETCORE_ENVIRONMENT` is `Development`
- **THEN** logs use Serilog's AnsiConsole theme with colored output and timestamps

### Requirement: SQL scripts embedded as assembly resources
The system SHALL embed all `Scripts/*.sql` files as assembly resources so no filesystem access is required at runtime (container-safe).

#### Scenario: Script discovery
- **WHEN** the runner starts
- **THEN** it discovers scripts from the executing assembly's embedded resources
- **AND** does not require scripts to exist on the container filesystem
