using Dapper;

namespace NomadRules.Ingest.Infrastructure;

// The canonical schema is owned entirely by NomadRules.DbMigrations (law_changes in V001, the dedup
// column/index + hash columns in V003). Ingest does NOT mutate the schema — it only VERIFIES the pieces it
// depends on are present, failing fast otherwise. This is deliberate: an earlier version self-added
// source_message_id here, which could race the runner's unconditional V003 ALTER and crash whichever ran
// second with "duplicate column name". A pure existence check has no such race.
public static class Migrations
{
    public static void Apply(Db db)
    {
        using var conn = db.Open();

        var tableExists = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'law_changes'") > 0;
        if (!tableExists)
            throw new InvalidOperationException(
                "law_changes table not found. Run db-migrations (V001) before starting ingest.");

        var cols = conn.Query<string>(
            "SELECT column_name FROM information_schema.columns WHERE table_name = 'law_changes'").AsList();
        if (!cols.Contains("source_message_id"))
            throw new InvalidOperationException(
                "law_changes.source_message_id missing. Run db-migrations (V003) before starting ingest.");
    }
}
