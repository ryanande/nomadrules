using Microsoft.Data.Sqlite;

namespace NomadRules.Api.Infrastructure;

// Schema is owned by NomadRules.DbMigrations (run before the API starts).
public class Db(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("Sqlite")
        ?? "Data Source=nomadrules.db";

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connStr);
        conn.Open();
        return conn;
    }
}
