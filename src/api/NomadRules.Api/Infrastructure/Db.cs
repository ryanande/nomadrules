using Dapper;
using Microsoft.Data.Sqlite;

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

    private readonly string _connStr = config.GetConnectionString("Sqlite")
        ?? "Data Source=nomadrules.db";

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connStr);
        conn.Open();
        return conn;
    }
}
