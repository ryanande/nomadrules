using Dapper;

namespace NomadRules.EmailDelivery.Infrastructure;

// The canonical renewal_alerts schema lives in NomadRules.DbMigrations (V002). This idempotent
// CREATE IF NOT EXISTS is the same shape and lets the worker run against a bare dev DB without the
// separate migration runner. Safe to run every startup.
// ponytail: duplicated DDL mirrors the summarizer's self-migrating pattern; when DbUp is the only
// path in prod this can drop to a table-exists guard.
public static class Migrations
{
    public static void Apply(Db db)
    {
        using var conn = db.Open();

        // Email delivery reads subscribers, law_changes, notifications — all API-owned. Fail with a clear
        // message instead of a cryptic "no such table" the first time we query them.
        var subscribersExist = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='subscribers'") > 0;
        if (!subscribersExist)
            throw new InvalidOperationException(
                "subscribers table not found. Run db-migrations (or start the API) to create the schema before email delivery.");

        // Delivery filters law_changes on `reviewed = 1` — a column the summarizer adds via its own
        // self-migration. Ensure it exists so delivery doesn't crash on a DB where the summarizer
        // hasn't run yet. Idempotent; matches the summarizer's DDL (0 = held for manual review).
        var lawCols = conn.Query<string>("SELECT name FROM pragma_table_info('law_changes')").AsList();
        if (lawCols.Count > 0 && !lawCols.Contains("reviewed"))
            conn.Execute("ALTER TABLE law_changes ADD COLUMN reviewed INTEGER NOT NULL DEFAULT 0");

        conn.Execute("""
            CREATE TABLE IF NOT EXISTS renewal_alerts (
              id TEXT PRIMARY KEY,
              subscriber_id TEXT NOT NULL REFERENCES subscribers(id),
              category TEXT NOT NULL,
              trigger_offset INTEGER NOT NULL,
              renewal_year INTEGER NOT NULL,
              sent_at TEXT,
              UNIQUE(subscriber_id, category, trigger_offset, renewal_year)
            )
            """);
    }
}
