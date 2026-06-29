using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace NomadRules.Summarizer.Summarization;

public class ClaudeSummarizer(AnthropicClient client, ClaudeOptions options, ILogger<ClaudeSummarizer> log)
{
    private readonly Lazy<string> _systemPrompt = new(() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Prompts", "SummarizeInsuranceChange.txt")));

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

        if (message.StopReason == "refusal")
            throw new SummarizationException("Claude refused to summarize the content");

        var json = message.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(json))
            throw new SummarizationException("Claude returned no text content");

        SummaryDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SummaryDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            log.LogError("Invalid JSON from Claude: {Body}", json);
            throw new SummarizationException($"Invalid JSON: {ex.Message}");
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Headline) || string.IsNullOrWhiteSpace(dto.Summary))
            throw new SummarizationException("Claude response missing headline or summary");

        var cost = Pricing.Cost(
            message.Usage.InputTokens, message.Usage.OutputTokens,
            options.InputCostPer1M, options.OutputCostPer1M);

        return new SummaryResult(
            dto.Headline.Trim(),
            dto.Summary.Trim(),
            Severity.Normalize(dto.Severity),
            message.Usage.InputTokens,
            message.Usage.OutputTokens,
            cost);
    }

    private static JsonElement J(object value) => JsonSerializer.SerializeToElement(value);

    private record SummaryDto(string? Headline, string? Summary, string? Severity);
}
