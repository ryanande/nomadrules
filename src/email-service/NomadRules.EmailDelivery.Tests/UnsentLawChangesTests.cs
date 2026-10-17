using Dapper;
using Microsoft.Extensions.Configuration;
using NomadRules.EmailDelivery.Delivery;
using NomadRules.EmailDelivery.Infrastructure;

namespace NomadRules.EmailDelivery.Tests;

public class UnsentLawChangesTests : IDisposable
{
    private readonly Db _db;
    private readonly string _tag = Guid.NewGuid().ToString("N");

    public UnsentLawChangesTests()
    {
        var conn = Environment.GetEnvironmentVariable("NOMADRULES_TEST_PG")
            ?? "Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules";
        _db = new Db(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = conn })
            .Build());
    }

    [Fact]
    public async Task UnsentLawChanges_IncludesFederalButNotOtherStates()
    {
        Insert("tx", "TX");
        Insert("sd", "SD");
        Insert("us", "US");

        var rows = await new DeliveryRepository(_db).UnsentLawChangesAsync($"sub-{_tag}", "SD", urgentOnly: false);

        var mine = rows.Select(r => r.Id).Where(id => id.EndsWith(_tag)).ToList();
        Assert.Contains($"sd-{_tag}", mine);
        Assert.Contains($"us-{_tag}", mine);
        Assert.DoesNotContain($"tx-{_tag}", mine);
    }

    private void Insert(string key, string state)
    {
        using var conn = _db.Open();
        conn.Execute("""
            INSERT INTO law_changes (id, source_id, url, raw_content, state, detected_at, processed_at, headline, summary, severity, reviewed)
            VALUES (@id, @src, 'https://example.test', 'raw', @state, '2999-01-01T00:00:00Z', '2999-01-01T00:00:00Z', 'h', 's', 'routine', 1)
            """, new { id = $"{key}-{_tag}", src = $"unsent-test-{_tag}", state });
    }

    public void Dispose()
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM law_changes WHERE source_id = @src", new { src = $"unsent-test-{_tag}" });
    }
}
