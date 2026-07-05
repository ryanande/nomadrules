using Npgsql;

namespace NomadRules.Ingest.Infrastructure;

// ponytail: mirrors the summarizer's Db helper rather than sharing a project — v0.1 keeps the worker deployable on its own.
public class Db(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=nomadrules;Username=nomadrules;Password=nomadrules";

    public NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        return conn;
    }
}
