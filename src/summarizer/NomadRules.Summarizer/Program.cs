using System.Diagnostics;
using Anthropic;
using NomadRules.Summarizer.Infrastructure;
using NomadRules.Summarizer.Summarization;
using NomadRules.Summarizer.Workers;

if (args.Contains("--selfcheck"))
{
    SelfCheck.Run();
    return;
}

var builder = Host.CreateApplicationBuilder(args);

var claude = builder.Configuration.GetSection("Claude").Get<ClaudeOptions>() ?? new();
var summarizer = builder.Configuration.GetSection("Summarizer").Get<SummarizerOptions>() ?? new();

builder.Services.AddSingleton(claude);
builder.Services.AddSingleton(summarizer);
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton(_ =>
    string.IsNullOrWhiteSpace(claude.ApiKey) ? new AnthropicClient() : new AnthropicClient { ApiKey = claude.ApiKey });
builder.Services.AddSingleton<LawChangeRepository>();
builder.Services.AddSingleton<ClaudeSummarizer>();
builder.Services.AddHostedService<SummarizerWorker>();

var host = builder.Build();

// Fail fast with clear messages instead of surfacing misconfig as endless retries/fallbacks later.
if (!File.Exists(ClaudeSummarizer.PromptPath))
    Fatal($"summarizer prompt not found at {ClaudeSummarizer.PromptPath}");

if (string.IsNullOrWhiteSpace(claude.ApiKey) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
    Fatal("no Claude API key configured (set Claude:ApiKey or the ANTHROPIC_API_KEY env var)");

try
{
    Migrations.Apply(host.Services.GetRequiredService<Db>());
}
catch (Exception ex)
{
    Fatal(ex.Message);
}

host.Run();

static void Fatal(string message)
{
    Console.Error.WriteLine($"FATAL: {message}");
    Environment.Exit(1);
}

// Minimal runnable check for the pure logic — `dotnet run -- --selfcheck`. No test framework.
static class SelfCheck
{
    public static void Run()
    {
        // Severity normalization
        Trap(Severity.Normalize("URGENT") == "urgent");
        Trap(Severity.Normalize(" Routine ") == "routine");
        Trap(Severity.Normalize("nonsense") == "routine"); // unknown -> safe default
        Trap(Severity.Normalize(null) == "routine");

        // Cost: 1M in @ $5 + 1M out @ $25 = $30
        Trap(Pricing.Cost(1_000_000, 1_000_000, 5.0, 25.0) == 30.0);
        Trap(Pricing.Cost(0, 0, 5.0, 25.0) == 0.0);

        // Review gate: first 10 held, 11th+ auto
        Trap(ReviewGate.ShouldHold(0, 10));
        Trap(ReviewGate.ShouldHold(9, 10));
        Trap(!ReviewGate.ShouldHold(10, 10));

        Console.WriteLine("selfcheck OK");
    }

    private static void Trap(bool condition)
    {
        if (!condition) throw new Exception("selfcheck FAILED");
    }
}
