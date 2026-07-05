using Microsoft.Data.Sqlite;

namespace NomadRules.Ingest.Infrastructure;

// ponytail: mirrors the summarizer's Db helper rather than sharing a project — v0.1 keeps the worker deployable on its own.
public class Db(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("Sqlite")
        ?? "Data Source=nomadrules.db";

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connStr);
        conn.Open();
        // WAL + busy timeout so ingest, summarizer, and the API can write the shared file without "database is locked".
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        return conn;
    }
}
