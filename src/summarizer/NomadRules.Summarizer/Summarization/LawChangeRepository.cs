using Dapper;
using NomadRules.Summarizer.Infrastructure;

namespace NomadRules.Summarizer.Summarization;

public class LawChangeRepository(Db db)
{
    private static string Now() => DateTime.UtcNow.ToString("o");

    // Next unprocessed row that has raw content, hasn't exhausted its attempts, and isn't deferred (quota or retry backoff).
    // ponytail: no row locking — v0.1 runs a single worker instance.
    public async Task<LawChangeRow?> ClaimNextAsync(int attemptsAllowed)
    {
        using var conn = db.Open();
        return await conn.QueryFirstOrDefaultAsync<LawChangeRow>("""
            SELECT id AS Id, raw_content AS RawContent, retry_count AS RetryCount
            FROM law_changes
            WHERE raw_content IS NOT NULL
              AND processed_at IS NULL
              AND retry_count < @attemptsAllowed
              AND (next_attempt_at IS NULL OR next_attempt_at <= @now)
            ORDER BY detected_at
            LIMIT 1
            """, new { attemptsAllowed, now = Now() });
    }

    // Count of successful summaries for the first-N review gate. Excludes fallback rows (summary IS NULL),
    // so failed rows don't consume review slots.
    public async Task<int> CountSummarizedAsync()
    {
        using var conn = db.Open();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM law_changes WHERE processed_at IS NOT NULL AND summary IS NOT NULL");
    }

    public async Task MarkSuccessAsync(string id, SummaryResult r, bool reviewed)
    {
        // Atomic: a crash between the update and the cost insert must not leave a processed row with no cost record.
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("""
            UPDATE law_changes
            SET headline = @Headline, summary = @Summary, severity = @Severity,
                processed_at = @now, reviewed = @reviewed, next_attempt_at = NULL
            WHERE id = @id
            """, new { r.Headline, r.Summary, r.Severity, now = Now(), reviewed = reviewed ? 1 : 0, id }, tx);
        await conn.ExecuteAsync("""
            INSERT INTO summarizer_costs (id, law_change_id, input_tokens, output_tokens, cost_usd, created_at)
            VALUES (@cid, @id, @InputTokens, @OutputTokens, @CostUsd, @now)
            """, new { cid = Guid.NewGuid().ToString(), id, r.InputTokens, r.OutputTokens, r.CostUsd, now = Now() }, tx);
        tx.Commit();
    }

    // Records Claude token spend for a law change — for successes and for billed-but-unusable responses alike.
    public async Task InsertCostAsync(string lawChangeId, long inputTokens, long outputTokens, double costUsd)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("""
            INSERT INTO summarizer_costs (id, law_change_id, input_tokens, output_tokens, cost_usd, created_at)
            VALUES (@cid, @lawChangeId, @inputTokens, @outputTokens, @costUsd, @now)
            """, new { cid = Guid.NewGuid().ToString(), lawChangeId, inputTokens, outputTokens, costUsd, now = Now() });
    }

    // Bumps retry_count and defers the next attempt (exponential backoff). Picked up again once next_attempt_at passes.
    public async Task IncrementRetryAsync(string id, int backoffSeconds)
    {
        using var conn = db.Open();
        var until = DateTime.UtcNow.AddSeconds(backoffSeconds).ToString("o");
        await conn.ExecuteAsync(
            "UPDATE law_changes SET retry_count = retry_count + 1, next_attempt_at = @until WHERE id = @id",
            new { until, id });
    }

    // Retries exhausted: finalize with a fallback headline and hold for manual review. Raw content is kept.
    public async Task MarkFallbackAsync(string id)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("""
            UPDATE law_changes
            SET headline = '[Unable to summarize - raw content follows]',
                summary = NULL, severity = 'routine', processed_at = @now, reviewed = 0
            WHERE id = @id
            """, new { now = Now(), id });
    }

    // 429: leave unprocessed but defer the next attempt without burning a retry.
    public async Task DeferForQuotaAsync(string id, int backoffMinutes)
    {
        using var conn = db.Open();
        var until = DateTime.UtcNow.AddMinutes(backoffMinutes).ToString("o");
        await conn.ExecuteAsync(
            "UPDATE law_changes SET next_attempt_at = @until WHERE id = @id", new { until, id });
    }
}
