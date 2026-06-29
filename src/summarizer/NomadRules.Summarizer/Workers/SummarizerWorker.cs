using Anthropic.Exceptions;
using NomadRules.Summarizer.Summarization;

namespace NomadRules.Summarizer.Workers;

public class SummarizerWorker(
    LawChangeRepository repo,
    ClaudeSummarizer summarizer,
    SummarizerOptions options,
    ILogger<SummarizerWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Summarizer worker started (poll {Poll}s, maxRetries {Max})",
            options.PollSeconds, options.MaxRetries);

        var attemptsAllowed = options.MaxRetries + 1; // 1 initial + N retries

        while (!stoppingToken.IsCancellationRequested)
        {
            LawChangeRow? row;
            try
            {
                row = await repo.ClaimNextAsync(attemptsAllowed);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to claim next law change");
                await Sleep(stoppingToken);
                continue;
            }

            if (row is null)
            {
                await Sleep(stoppingToken);
                continue;
            }

            await ProcessAsync(row, stoppingToken);
            // Loop straight back to drain any backlog; only Sleep when the queue is empty.
        }
    }

    private async Task ProcessAsync(LawChangeRow row, CancellationToken stoppingToken)
    {
        try
        {
            var processedBefore = await repo.CountProcessedAsync();
            var result = await summarizer.SummarizeAsync(row.RawContent, stoppingToken);

            var hold = ReviewGate.ShouldHold(processedBefore, options.ReviewThreshold);
            await repo.MarkSuccessAsync(row.Id, result, reviewed: !hold);

            log.LogInformation(
                "Summarized {Id}: severity={Severity} tokens={In}/{Out} cost=${Cost} reviewed={Reviewed}",
                row.Id, result.Severity, result.InputTokens, result.OutputTokens, result.CostUsd, !hold);

            if (hold && processedBefore + 1 == options.ReviewThreshold)
                // ponytail: alerting Ryan/Jenn is the email service's job; we log the seam. Email delivery filters on reviewed=1.
                log.LogWarning("First {N} summaries ready for review — manual approval required before delivery",
                    options.ReviewThreshold);
        }
        catch (AnthropicRateLimitException)
        {
            log.LogWarning("Claude quota exceeded, queue building — deferring {Id} for {Min}min",
                row.Id, options.QuotaBackoffMinutes);
            await repo.DeferForQuotaAsync(row.Id, options.QuotaBackoffMinutes);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            await HandleFailureAsync(row, "Claude API timeout");
        }
        catch (SummarizationException ex)
        {
            // The response was billed even though it was unusable — record the spend before retrying.
            if (ex.InputTokens > 0 || ex.OutputTokens > 0)
                await repo.InsertCostAsync(row.Id, ex.InputTokens, ex.OutputTokens, ex.CostUsd);
            await HandleFailureAsync(row, ex.Message);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Unexpected (e.g. 5xx after the SDK's own retries). Never crash the worker.
            await HandleFailureAsync(row, ex.Message);
        }
    }

    private async Task HandleFailureAsync(LawChangeRow row, string reason)
    {
        var attempt = row.RetryCount + 1;
        if (attempt > options.MaxRetries)
        {
            log.LogError("Giving up on {Id} after {Attempt} attempts ({Reason}) — finalizing with fallback headline",
                row.Id, attempt, reason);
            await repo.MarkFallbackAsync(row.Id);
        }
        else
        {
            var backoff = (int)Math.Pow(2, attempt) * options.PollSeconds; // exponential
            log.LogWarning("Summarize failed for {Id} ({Reason}); retry {Attempt}/{Max} in {Backoff}s",
                row.Id, reason, attempt, options.MaxRetries, backoff);
            await repo.IncrementRetryAsync(row.Id, backoff);
        }
    }

    private async Task Sleep(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds), ct); }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}
