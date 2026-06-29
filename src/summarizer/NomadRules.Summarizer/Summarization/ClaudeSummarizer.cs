using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace NomadRules.Summarizer.Summarization;

public class ClaudeSummarizer(AnthropicClient client, ClaudeOptions options, ILogger<ClaudeSummarizer> log)
{
    public static readonly string PromptPath =
        Path.Combine(AppContext.BaseDirectory, "Prompts", "SummarizeInsuranceChange.txt");

    private readonly Lazy<string> _systemPrompt = new(() => File.ReadAllText(PromptPath));

    private static readonly Dictionary<string, JsonElement> Schema = new()
    {
        ["type"] = J("object"),
        ["additionalProperties"] = J(false),
        ["required"] = J(new[] { "headline", "summary", "severity" }),
        ["properties"] = J(new
        {
            headline = new { type = "string" },
            summary = new { type = "string" },
            severity = new { type = "string", @enum = new[] { "urgent", "routine", "informational" } },
        }),
    };

    // Throws SummarizationException on bad output; OperationCanceledException on timeout;
    // AnthropicRateLimitException (429) propagates so the worker can defer.
    public async Task<SummaryResult> SummarizeAsync(string rawContent, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        var message = await client.Messages.Create(new MessageCreateParams
        {
            Model = options.Model,
            MaxTokens = 1024,
            System = _systemPrompt.Value,
            OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = Schema } },
            Messages =
            [
                new() { Role = Role.User, Content = $"Here is the law change:\n\n{rawContent}" },
            ],
        }, cancellationToken: timeoutCts.Token);

        // Tokens are billed even when the response is unusable — carry them so the worker still cost-logs the failure.
        var inTok = message.Usage.InputTokens;
        var outTok = message.Usage.OutputTokens;
        var cost = Pricing.Cost(inTok, outTok, options.InputCostPer1M, options.OutputCostPer1M);

        if (message.StopReason == "refusal")
            throw new SummarizationException("Claude refused to summarize the content", inTok, outTok, cost);

        var json = message.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(json))
            throw new SummarizationException("Claude returned no text content", inTok, outTok, cost);

        SummaryDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SummaryDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            log.LogError("Invalid JSON from Claude: {Body}", json);
            throw new SummarizationException($"Invalid JSON: {ex.Message}", inTok, outTok, cost);
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Headline) || string.IsNullOrWhiteSpace(dto.Summary))
            throw new SummarizationException("Claude response missing headline or summary", inTok, outTok, cost);

        return new SummaryResult(
            dto.Headline.Trim(),
            dto.Summary.Trim(),
            Severity.Normalize(dto.Severity),
            inTok,
            outTok,
            cost);
    }

    private static JsonElement J(object value) => JsonSerializer.SerializeToElement(value);

    private record SummaryDto(string? Headline, string? Summary, string? Severity);
}
