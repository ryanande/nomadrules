using Dapper;
using NomadRules.Api.Features.LawChanges;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Tests;

public class LawChangeFeedTests : IDisposable
{
    private readonly Db _db = TestDb.Create();
    private readonly string _tag = Guid.NewGuid().ToString("N");

    [Fact]
    public async Task GetFeed_WithNullState_ReturnsEmptyEvenWhenFederalItemsExist()
    {
        Insert("us", "US");

        var (items, total) = await new LawChangeService(_db).GetFeedAsync(null, 20, 0);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task GetFeed_IncludesFederalItemsButNotOtherStates()
    {
        Insert("tx", "TX");
        Insert("sd", "SD");
        Insert("us", "US");

        var (items, total) = await new LawChangeService(_db).GetFeedAsync("SD", 1000, 0);

        var mine = items.Where(i => i.SourceId == $"feed-test-{_tag}").Select(i => i.Id).ToList();
        Assert.Contains($"sd-{_tag}", mine);
        Assert.Contains($"us-{_tag}", mine);
        Assert.DoesNotContain($"tx-{_tag}", mine);
        Assert.True(total >= 2);
    }

    private void Insert(string key, string state)
    {
        using var conn = _db.Open();
        conn.Execute("""
            INSERT INTO law_changes (id, source_id, url, raw_content, state, detected_at, processed_at, headline, summary, severity, reviewed)
            VALUES (@id, @src, 'https://example.test', 'raw', @state, '2999-01-01T00:00:00Z', '2999-01-01T00:00:00Z', 'h', 's', 'routine', 1)
            """, new { id = $"{key}-{_tag}", src = $"feed-test-{_tag}", state });
    }

    public void Dispose()
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM law_changes WHERE source_id = @src", new { src = $"feed-test-{_tag}" });
    }
}
