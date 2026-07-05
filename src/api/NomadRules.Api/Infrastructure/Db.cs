using Dapper;
using Npgsql;

namespace NomadRules.Api.Infrastructure;

// Schema is owned by NomadRules.DbMigrations (run before the API starts).
public class Db(IConfiguration config)
{
    static Db()
    {
        // Every table uses snake_case columns; without this, Dapper only matches
        // exact-name columns and every snake_case column deserializes to null.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private readonly string _connStr = config.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=nomadrules;Username=nomadrules;Password=nomadrules";

    public NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        return conn;
    }
}
