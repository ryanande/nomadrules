using Dapper;

namespace NomadRules.Summarizer.Infrastructure;

// Additive, summarizer-owned schema on the shared SQLite file. Idempotent — safe to run every startup.
// ponytail: hand-rolled because SQLite lacks "ADD COLUMN IF NOT EXISTS"; swap for DbUp when migrations land (see openspec/changes/db-migrations-dbup).
public static class Migrations
{
    public static void Apply(Db db)
    {
        using var conn = db.Open();

        var cols = conn.Query<string>("SELECT name FROM pragma_table_info('law_changes')").AsList();

        void AddColumn(string name, string ddl)
        {
            if (!cols.Contains(name))
                conn.Execute($"ALTER TABLE law_changes ADD COLUMN {ddl}");
        }

        AddColumn("retry_count", "retry_count INTEGER NOT NULL DEFAULT 0");
        AddColumn("reviewed", "reviewed INTEGER NOT NULL DEFAULT 0");        // 0 = held for manual review
        AddColumn("hallucination", "hallucination INTEGER NOT NULL DEFAULT 0"); // flagged during manual review
        AddColumn("next_attempt_at", "next_attempt_at TEXT");               // set when deferred after a 429

        conn.Execute("""
            CREATE TABLE IF NOT EXISTS summarizer_costs (
              id TEXT PRIMARY KEY,
              law_change_id TEXT NOT NULL REFERENCES law_changes(id),
              input_tokens INTEGER NOT NULL,
              output_tokens INTEGER NOT NULL,
              cost_usd REAL NOT NULL,
              created_at TEXT NOT NULL
            )
            """);
    }
}
