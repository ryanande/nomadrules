## REMOVED Requirements

### Requirement: API initializes database schema on startup
**Reason**: Schema ownership transferred to `NomadRules.DbMigrations`. The API should trust the schema is already applied before it starts.
**Migration**: Run `NomadRules.DbMigrations` before starting the API. In K8s this is an init container; locally run `dotnet run --project src/db-migrations/NomadRules.DbMigrations` first.
