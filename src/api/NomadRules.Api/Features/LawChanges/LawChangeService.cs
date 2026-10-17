using Dapper;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Features.LawChanges;

public class LawChange
{
    public string Id { get; set; } = default!;
    public string SourceId { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string? Headline { get; set; }
    public string? Summary { get; set; }
    public string? Severity { get; set; }
    public string State { get; set; } = default!;
    public string DetectedAt { get; set; } = default!;
    public string? ProcessedAt { get; set; }
}

public class LawChangeService(Db db)
{
    // Federal changes are stored with state 'US' and shown to every subscriber alongside their own state's.
    // A null state (subscriber not yet onboarded) gets an empty feed; the portal gates the feed behind onboarding.
    public async Task<(List<LawChange> items, int total)> GetFeedAsync(string? state, int limit, int offset)
    {
        if (state is null) return ([], 0);

        using var conn = db.Open();

        var total = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM law_changes WHERE state IN (@state, 'US') AND processed_at IS NOT NULL",
            new { state });

        var items = (await conn.QueryAsync<LawChange>("""
            SELECT id, source_id, url, headline, summary, severity, state, detected_at, processed_at
            FROM law_changes
            WHERE state IN (@state, 'US') AND processed_at IS NOT NULL
            ORDER BY detected_at DESC
            LIMIT @limit OFFSET @offset
            """, new { state, limit, offset })).AsList();

        return (items, total);
    }
}
