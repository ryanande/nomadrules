using System.Reflection;
using Dapper;
using DbUp;
using Npgsql;

namespace NomadRules.DbMigrations.Tests;

public class MigrationScriptsTests : IDisposable
{
    private readonly NpgsqlConnectionStringBuilder _admin;
    private readonly string _dbName = "nr_migtest_" + Guid.NewGuid().ToString("N");
    private readonly string _connStr;

    public MigrationScriptsTests()
    {
        var baseConn = Environment.GetEnvironmentVariable("NOMADRULES_TEST_PG")
            ?? "Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules";
        _admin = new NpgsqlConnectionStringBuilder(baseConn) { Database = "postgres" };
        _connStr = new NpgsqlConnectionStringBuilder(baseConn) { Database = _dbName }.ConnectionString;

        using var conn = new NpgsqlConnection(_admin.ConnectionString);
        conn.Execute($"CREATE DATABASE {_dbName}");
    }

    private DbUp.Engine.DatabaseUpgradeResult Migrate() => DeployChanges.To
        .PostgresqlDatabase(_connStr)
        .WithScriptsEmbeddedInAssembly(Assembly.Load("NomadRules.DbMigrations"))
        .LogToNowhere()
        .Build()
        .PerformUpgrade();

    [Fact]
    public void FreshDatabase_AppliesScriptsInOrder_AndSeedsStates()
    {
        var result = Migrate();

        Assert.True(result.Successful, result.Error?.ToString());
        Assert.Equal(["0001_initial_schema.sql", "0002_lookup_data.sql"],
            result.Scripts.Select(s => s.Name.Split(".Scripts.")[1]));

        using var conn = new NpgsqlConnection(_connStr);
        var states = conn.Query<(string Code, string Name, bool IsDomicile)>(
            "SELECT code AS Code, name AS Name, is_domicile AS IsDomicile FROM states ORDER BY code").ToList();
        Assert.Equal(
            [("SD", "South Dakota", true), ("TX", "Texas", true), ("US", "United States", false)],
            states);
    }

    [Fact]
    public void NewSubscriber_HasNullState_UntilOnboarded()
    {
        Assert.True(Migrate().Successful);

        using var conn = new NpgsqlConnection(_connStr);
        conn.Execute("INSERT INTO subscribers (id, email) VALUES ('s1', 's1@example.test')");

        Assert.Null(conn.ExecuteScalar<string?>("SELECT state FROM subscribers WHERE id = 's1'"));
    }

    [Fact]
    public void SecondRun_AppliesNothing()
    {
        Assert.True(Migrate().Successful);

        var again = Migrate();

        Assert.True(again.Successful);
        Assert.Empty(again.Scripts);
    }

    public void Dispose()
    {
        using var conn = new NpgsqlConnection(_admin.ConnectionString);
        conn.Execute($"DROP DATABASE IF EXISTS {_dbName} WITH (FORCE)");
    }
}
