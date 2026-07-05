namespace NomadRules.Summarizer.Summarization;

public class SummarizerOptions
{
    public int PollSeconds { get; set; } = 15;
    public int MaxRetries { get; set; } = 2;
    public int QuotaBackoffMinutes { get; set; } = 60;
    public int ReviewThreshold { get; set; } = 10;
}

public class ClaudeOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-4-8";
    public double InputCostPer1M { get; set; } = 5.0;
    public double OutputCostPer1M { get; set; } = 25.0;
    public int TimeoutSeconds { get; set; } = 30;
}

// A row claimed for summarization. RetryCount is int to match Postgres INTEGER (Dapper's record/constructor
// materialization requires an exact type match — unlike class property assignment, which tolerates numeric coercion).
public record LawChangeRow(string Id, string RawContent, int RetryCount);

// Parsed Claude output plus usage/cost for one summary.
public record SummaryResult(
    string Headline,
    string Summary,
    string Severity,
    long InputTokens,
    long OutputTokens,
    double CostUsd);

// Raised when Claude returns something we can't use (bad JSON, refusal, empty).
// Carries token usage when a billed response came back (so failures are still cost-logged); zero when none did (timeout/429).
public class SummarizationException(string message, long inputTokens = 0, long outputTokens = 0, double costUsd = 0)
    : Exception(message)
{
    public long InputTokens { get; } = inputTokens;
    public long OutputTokens { get; } = outputTokens;
    public double CostUsd { get; } = costUsd;
}

public static class Severity
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { "urgent", "routine", "informational" };

    // Coerce an unknown/missing severity to the safe default rather than failing the whole summary.
    public static string Normalize(string? raw)
    {
        var s = raw?.Trim().ToLowerInvariant();
        return s is not null && Allowed.Contains(s) ? s : "routine";
    }
}

public static class Pricing
{
    public static double Cost(long inputTokens, long outputTokens, double inPer1M, double outPer1M)
        => Math.Round(inputTokens / 1_000_000d * inPer1M + outputTokens / 1_000_000d * outPer1M, 6);
}

public static class ReviewGate
{
    // First `threshold` summaries are held (reviewed = false) for manual approval; the rest auto-deliver.
    // `alreadyProcessed` is the count of rows summarized before this one.
    public static bool ShouldHold(int alreadyProcessed, int threshold) => alreadyProcessed < threshold;
}
