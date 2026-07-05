using Dapper;

namespace NomadRules.Ingest.Infrastructure;

// The canonical schema lives in NomadRules.DbMigrations (law_changes in V001, source_message_id + its
// unique index in V003). This idempotent guard lets the worker run against a dev DB without the separate
// migration runner. Safe to run every startup.
// ponytail: duplicated DDL mirrors the summarizer's self-migrating pattern; when DbUp is the only prod
// path this can drop to a table-exists guard.
public static class Migrations
{
    public static void Apply(Db db)
    {
        using var conn = db.Open();

        // Ingest writes law_changes — an API-owned table. Fail with a clear message instead of a cryptic
        // "no such table" the first time we insert.
        var exists = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='law_changes'") > 0;
        if (!exists)
            throw new InvalidOperationException(
                "law_changes table not found. Run db-migrations (or start the API) to create the schema before ingest.");

        // Dedup key (V003). Ensure the column + unique index exist so idempotent insert works on a bare DB.
        var cols = conn.Query<string>("SELECT name FROM pragma_table_info('law_changes')").AsList();
        if (!cols.Contains("source_message_id"))
            conn.Execute("ALTER TABLE law_changes ADD COLUMN source_message_id TEXT");
        conn.Execute("""
            CREATE UNIQUE INDEX IF NOT EXISTS ux_law_changes_source_message_id
              ON law_changes(source_message_id) WHERE source_message_id IS NOT NULL
            """);
    }
}
