using NomadRules.EmailDelivery.Delivery;
using NomadRules.EmailDelivery.Infrastructure;
using NomadRules.EmailDelivery.Workers;
using Serilog;
using Serilog.Enrichers.Span;

SQLitePCL.Batteries_V2.Init(); // register the native e_sqlite3 provider

if (args.Contains("--selfcheck"))
{
    SelfCheck.Run();
    return 0;
}

var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
var isDev = env.Equals("Development", StringComparison.OrdinalIgnoreCase);

// Serilog: JSON in prod, colored console in dev, trace-enriched — mirrors the db-migrations runner.
var logConfig = new LoggerConfiguration().Enrich.FromLogContext().Enrich.WithSpan();
logConfig = isDev
    ? logConfig.WriteTo.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
    : logConfig.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
Log.Logger = logConfig.CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog();

var resend = builder.Configuration.GetSection("Resend").Get<ResendOptions>() ?? new();
resend.ApiKey = string.IsNullOrWhiteSpace(resend.ApiKey)
    ? Environment.GetEnvironmentVariable("RESEND_API_KEY")
    : resend.ApiKey;
var delivery = builder.Configuration.GetSection("Delivery").Get<DeliveryOptions>() ?? new();

builder.Services.AddSingleton(resend);
builder.Services.AddSingleton(delivery);
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<DeliveryRepository>();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.AddSingleton<DeliveryWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DeliveryWorker>());

var host = builder.Build();

// Fail fast on misconfig instead of surfacing it later as silent no-sends.
if (string.IsNullOrWhiteSpace(resend.ApiKey))
    return Fatal("no Resend API key configured (set Resend:ApiKey or the RESEND_API_KEY env var)");

try
{
    Migrations.Apply(host.Services.GetRequiredService<Db>());
}
catch (Exception ex)
{
    return Fatal(ex.Message);
}

// --run-now: one immediate pass (local testing + K8s CronJob), then exit.
if (args.Contains("--run-now"))
{
    await host.Services.GetRequiredService<DeliveryWorker>().RunOnceAsync(CancellationToken.None);
    await Log.CloseAndFlushAsync();
    return 0;
}

host.Run();
await Log.CloseAndFlushAsync();
return 0;

static int Fatal(string message)
{
    Console.Error.WriteLine($"FATAL: {message}");
    return 1;
}

// Minimal runnable check for the pure logic — `dotnet run -- --selfcheck`. No test framework.
static class SelfCheck
{
    public static void Run()
    {
        var today = new DateOnly(2026, 7, 4);

        // Anchor, null day -> falls back to the 1st (prior month-only behavior, unchanged):
        Trap(RenewalTriggers.Anchor(9, null, today) == new DateOnly(2026, 9, 1));   // future month this year
        Trap(RenewalTriggers.Anchor(3, null, today) == new DateOnly(2027, 3, 1));   // passed -> next year
        Trap(RenewalTriggers.Anchor(7, null, today) == new DateOnly(2027, 7, 1));   // current month, 1st passed
        // Anchor with a day -> day-accurate:
        Trap(RenewalTriggers.Anchor(9, 20, today) == new DateOnly(2026, 9, 20));    // month+day this year
        Trap(RenewalTriggers.Anchor(7, 2, today) == new DateOnly(2027, 7, 2));      // day already passed -> next year
        Trap(RenewalTriggers.Anchor(7, 4, today) == new DateOnly(2026, 7, 4));      // day == today -> today (not passed)
        // Feb 29 renewal clamps to Feb 28 in a non-leap anchor year (2027):
        Trap(RenewalTriggers.Anchor(2, 29, today) == new DateOnly(2027, 2, 28));

        // Alert copy names the date when a day is known, stays vague when it isn't.
        Trap(Templates.RenewalAlert("insurance", 60, 9, 20).Body.Contains("on September 20"));
        Trap(Templates.RenewalAlert("insurance", 60, 9, null).Body.Contains("around September"));

        // Offset boundaries: exactly 60/30/7 days out trigger; off-by-one does not.
        var anchor = new DateOnly(2026, 9, 2); // 60 days after 2026-07-04
        Trap(RenewalTriggers.DueOffset(anchor, today) == 60);
        Trap(RenewalTriggers.DueOffset(new DateOnly(2026, 8, 3), today) == 30);
        Trap(RenewalTriggers.DueOffset(new DateOnly(2026, 7, 11), today) == 7);
        Trap(RenewalTriggers.DueOffset(new DateOnly(2026, 7, 12), today) is null); // 8 days -> no trigger
        Trap(RenewalTriggers.DueOffset(today, today) is null);                     // 0 days -> no trigger

        // Idempotency keys: deterministic, and distinct across offset/year/change (digest exclusion depends on this).
        Trap(IdempotencyKeys.RenewalAlert("s1", "insurance", 60, 2026) == "s1:insurance:60:2026");
        Trap(IdempotencyKeys.RenewalAlert("s1", "insurance", 60, 2026)
             != IdempotencyKeys.RenewalAlert("s1", "insurance", 30, 2026));
        // Digest key: order-independent (same change set -> same key, so a crash-retry is deduped by Resend),
        // and distinct when the change set differs.
        Trap(IdempotencyKeys.Digest("s1", ["c2", "c1"]) == IdempotencyKeys.Digest("s1", ["c1", "c2"]));
        Trap(IdempotencyKeys.Digest("s1", ["c1"]) != IdempotencyKeys.Digest("s1", ["c1", "c2"]));

        Trap(IdempotencyKeys.Notification("s1", "c1") == "s1:c1");
        Trap(IdempotencyKeys.Notification("s1", "c1") != IdempotencyKeys.Notification("s1", "c2"));

        Console.WriteLine("selfcheck OK");
    }

    private static void Trap(bool condition)
    {
        if (!condition) throw new Exception("selfcheck FAILED");
    }
}
