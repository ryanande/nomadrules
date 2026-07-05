using System.Diagnostics;
using System.Reflection;
using DbUp;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Enrichers.Span;
using NomadRules.DbMigrations;

const string ServiceName = "db-migration-runner";
const string PushgatewayJob = "db_migration_runner";

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
var isDev = env.Equals("Development", StringComparison.OrdinalIgnoreCase);
var instance = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;

// --- Logging (Serilog): JSON in prod, colored console in dev; enriched with trace/span id ---
var logConfig = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithSpan();
logConfig = isDev
    ? logConfig.WriteTo.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
    : logConfig.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
Log.Logger = logConfig.CreateLogger();

// --- ActivitySource for tracing; OTLP exporter wired only if endpoint is set ---
using var activitySource = new ActivitySource(ServiceName);
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
TracerProvider? tracerProvider = null;
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName))
        .AddSource(ServiceName)
        .AddOtlpExporter()
        .Build();
}
else
{
    Log.Information("OTEL_EXPORTER_OTLP_ENDPOINT unset — trace export disabled");
}

// --- Connection string: env var first, then appsettings, else fatal ---
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? config.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connStr))
{
    Log.Fatal("No connection string: set POSTGRES_CONNECTION_STRING or ConnectionStrings:Postgres");
    await FlushAsync(tracerProvider);
    return 1;
}

// --- Metrics ---
var appliedCounter = Metrics.CreateCounter(
    "migration_scripts_applied_total", "Scripts applied this run", new CounterConfiguration
    {
        LabelNames = ["result"]
    });
var durationGauge = Metrics.CreateGauge(
    "migration_run_duration_seconds", "Duration of the migration run");
var timestampGauge = Metrics.CreateGauge(
    "migration_run_timestamp_seconds", "Unix timestamp of the last successful run");

var sw = Stopwatch.StartNew();
int exitCode;

using (var runSpan = activitySource.StartActivity("migration.run"))
{
    runSpan?.SetTag("db.system", "postgresql");
    try
    {
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connStr)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogTo(new SerilogUpgradeLog(activitySource))
            .Build();

        var pending = upgrader.GetScriptsToExecute().Count;
        if (pending == 0)
            Log.Information("No new migrations to apply");

        var result = upgrader.PerformUpgrade();

        if (result.Successful)
        {
            var count = result.Scripts.Count();
            appliedCounter.WithLabels("success").Inc(count);
            timestampGauge.SetToCurrentTimeUtc();
            Log.Information("Migration succeeded: {Count} script(s) applied", count);
            exitCode = 0;
        }
        else
        {
            appliedCounter.WithLabels("failure").Inc();
            runSpan?.SetStatus(ActivityStatusCode.Error, result.Error?.Message);
            Log.Error(result.Error, "Migration failed on script {Script}", result.ErrorScript?.Name);
            exitCode = 1;
        }
    }
    catch (Exception ex)
    {
        appliedCounter.WithLabels("failure").Inc();
        runSpan?.SetStatus(ActivityStatusCode.Error, ex.Message);
        Log.Fatal(ex, "Migration runner crashed");
        exitCode = 1;
    }
}

sw.Stop();
durationGauge.Set(sw.Elapsed.TotalSeconds);

// --- Push metrics (best-effort; never affects exit code) ---
await PushMetricsAsync(instance);
await FlushAsync(tracerProvider);
await Log.CloseAndFlushAsync();
return exitCode;

// ---------------------------------------------------------------------------

async Task PushMetricsAsync(string inst)
{
    var url = Environment.GetEnvironmentVariable("PUSHGATEWAY_URL");
    if (string.IsNullOrWhiteSpace(url))
    {
        Log.Information("PUSHGATEWAY_URL unset — metric push disabled");
        return;
    }
    try
    {
        using var ms = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(ms);
        ms.Position = 0;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var endpoint = $"{url.TrimEnd('/')}/metrics/job/{PushgatewayJob}/instance/{Uri.EscapeDataString(inst)}";
        var content = new StreamContent(ms);
        content.Headers.ContentType = new("text/plain") { CharSet = "utf-8" };
        var resp = await http.PutAsync(endpoint, content);
        resp.EnsureSuccessStatusCode();
        Log.Information("Pushed metrics to {Endpoint}", endpoint);
    }
    catch (Exception ex)
    {
        // ponytail: telemetry is best-effort — a dead Pushgateway must not fail a migration
        Log.Warning(ex, "Failed to push metrics to Pushgateway");
    }
}

static async Task FlushAsync(TracerProvider? tp)
{
    try { tp?.ForceFlush(5000); tp?.Dispose(); }
    catch (Exception ex) { Log.Warning(ex, "Failed to flush traces"); }
    await Task.CompletedTask;
}
