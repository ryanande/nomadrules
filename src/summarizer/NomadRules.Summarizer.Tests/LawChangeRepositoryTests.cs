using Dapper;
using Microsoft.Extensions.Configuration;
using NomadRules.Summarizer.Infrastructure;
using NomadRules.Summarizer.Summarization;

namespace NomadRules.Summarizer.Tests;

public class LawChangeRepositoryTests : IDisposable
{
    private const string DefaultConn =
        "Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules";

    private readonly Db _db;
    private readonly string _id = Guid.NewGuid().ToString();

    public LawChangeRepositoryTests()
    {
        var conn = Environment.GetEnvironmentVariable("NOMADRULES_TEST_PG") ?? DefaultConn;
        _db = new Db(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = conn })
            .Build());
    }

    [Fact]
    public async Task ClaimNext_ReturnsTheRowsOwnState()
    {
        using (var conn = _db.Open())
        {
            // detected_at sorts first so this row is the one claimed even if the dev DB holds other rows.
            await conn.ExecuteAsync("""
                INSERT INTO law_changes (id, source_id, url, raw_content, state, detected_at)
                VALUES (@id, 'test', 'https://example.test', 'raw text', 'SD', '0000-01-01T00:00:00Z')
                """, new { id = _id });
        }

        var row = await new LawChangeRepository(_db).ClaimNextAsync(attemptsAllowed: 5);

        Assert.NotNull(row);
        Assert.Equal(_id, row!.Id);
        Assert.Equal("SD", row.State);
    }

    public void Dispose()
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM law_changes WHERE id = @id", new { id = _id });
    }
}
