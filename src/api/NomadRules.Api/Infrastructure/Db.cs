using Dapper;
using Microsoft.Data.Sqlite;

namespace NomadRules.Api.Infrastructure;

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

    public async Task InitializeAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Infrastructure", "schema.sql"));
        using var conn = Open();
        await conn.ExecuteAsync(sql);
    }
}
