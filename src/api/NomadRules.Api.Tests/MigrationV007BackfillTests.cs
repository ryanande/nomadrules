using Dapper;
using NomadRules.Api.Features.Subscribers;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Tests;

public class MigrationV007BackfillTests : IDisposable
{
    private readonly Db _db = TestDb.Create();
    private readonly string _tag = Guid.NewGuid().ToString("N");

    private string Email(string s) => $"v007-{s}-{_tag}@example.test";

    [Fact]
    public async Task Backfill_NullsUnsupportedStates_AndKeepsSupportedOnes()
    {
        var svc = new SubscriberService(_db);
        var ca = await svc.CreateAsync(new RegisterRequest(Email("ca"), "CA", null, null, null, null));
        var sd = await svc.CreateAsync(new RegisterRequest(Email("sd"), "SD", null, null, null, null));
        var tx = await svc.CreateAsync(new RegisterRequest(Email("tx"), "TX", null, null, null, null));

        using (var conn = _db.Open())
            conn.Execute(File.ReadAllText(ScriptPath()));

        Assert.Null((await svc.GetByIdAsync(ca.Id))!.State);
        Assert.Equal("SD", (await svc.GetByIdAsync(sd.Id))!.State);
        Assert.Equal("TX", (await svc.GetByIdAsync(tx.Id))!.State);
    }

    private static string ScriptPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "db-migrations", "NomadRules.DbMigrations",
                "Scripts", "V007__subscriber_state_nullable.sql");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("V007 migration script not found above " + AppContext.BaseDirectory);
    }

    public void Dispose()
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM subscribers WHERE email LIKE @p", new { p = $"v007-%-{_tag}@example.test" });
    }
}
