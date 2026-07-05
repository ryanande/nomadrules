using NomadRules.Ingest.Infrastructure;
using NomadRules.Ingest.Ingest;
using NomadRules.Ingest.Workers;
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

// Serilog: JSON in prod, colored console in dev, trace-enriched — mirrors the other workers.
var logConfig = new LoggerConfiguration().Enrich.FromLogContext().Enrich.WithSpan();
logConfig = isDev
    ? logConfig.WriteTo.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
    : logConfig.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
Log.Logger = logConfig.CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog();

var ingest = builder.Configuration.GetSection("Ingest").Get<IngestOptions>() ?? new();
ingest.ServiceBusConnectionString = string.IsNullOrWhiteSpace(ingest.ServiceBusConnectionString)
    ? Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION_STRING")
    : ingest.ServiceBusConnectionString;

var isServiceBus = ingest.Transport.Equals("servicebus", StringComparison.OrdinalIgnoreCase);

builder.Services.AddSingleton(ingest);
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<IngestRepository>();
if (isServiceBus)
    builder.Services.AddSingleton<IMessageSource, ServiceBusSource>();
else
    builder.Services.AddSingleton<IMessageSource, LocalFileSource>();
builder.Services.AddSingleton<IngestWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IngestWorker>());

var host = builder.Build();

// Fail fast on misconfig instead of surfacing it as silent no-ingest.
if (isServiceBus && string.IsNullOrWhiteSpace(ingest.ServiceBusConnectionString))
    return Fatal("Transport=servicebus but no connection string (set Ingest:ServiceBusConnectionString or AZURE_SERVICE_BUS_CONNECTION_STRING)");

try
{
    Migrations.Apply(host.Services.GetRequiredService<Db>());
}
catch (Exception ex)
{
    return Fatal(ex.Message);
}

// --run-now: one drain pass (local testing + K8s CronJob against the file queue), then exit.
if (args.Contains("--run-now"))
{
    var n = await host.Services.GetRequiredService<IngestWorker>().DrainOnceAsync(CancellationToken.None);
    Log.Information("Drained {Count} message(s)", n);
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
        var msg = new LawChangeDetected(
            "LawChangeDetected", "msg-123", "tx-doi", "<raw>", "hashA", null,
            "http://tdi.tx", null, "insurance", "2026-07-02T00:00:00Z");

        var row = Mapping.ToRow(msg with { ContentHash = "hashA", PreviousHash = "hashPrev" }, "TX");
        Trap(row.SourceMessageId == "msg-123");        // dedup key is the producer's messageId
        Trap(row.Id != "msg-123");                     // our id is system-owned, not the messageId
        Trap(Guid.TryParse(row.Id, out _));
        Trap(row.State == "TX");                       // null message state -> MVP default
        Trap(row.SourceId == "tx-doi" && row.RawContent == "<raw>");
        Trap(row.ContentHash == "hashA" && row.PreviousHash == "hashPrev"); // hashes persisted, not dropped

        var withState = Mapping.ToRow(msg with { State = "CA" }, "TX");
        Trap(withState.State == "CA");                 // explicit state wins over default

        // Two deliveries of the same message map to the same dedup key (idempotency depends on this).
        Trap(Mapping.ToRow(msg, "TX").SourceMessageId == Mapping.ToRow(msg, "TX").SourceMessageId);

        // Parsing: valid camelCase JSON parses; malformed / missing-id payloads return null.
        Trap(Mapping.TryParse(
            """{"messageType":"LawChangeDetected","messageId":"m1","sourceId":"s","rawContent":"r","contentHash":"h","previousHash":null,"url":"u","state":"TX","category":"insurance","detectedAt":"2026-07-02"}""")
            is { MessageId: "m1", State: "TX" });
        Trap(Mapping.TryParse("{ not json") is null);
        Trap(Mapping.TryParse("""{"messageType":"LawChangeDetected"}""") is null); // missing messageId/sourceId

        Console.WriteLine("selfcheck OK");
    }

    private static void Trap(bool condition)
    {
        if (!condition) throw new Exception("selfcheck FAILED");
    }
}
