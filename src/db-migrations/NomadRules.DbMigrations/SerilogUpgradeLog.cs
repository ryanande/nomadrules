using System.Diagnostics;
using DbUp.Engine.Output;
using Serilog;

namespace NomadRules.DbMigrations;

/// <summary>
/// Bridges DbUp's IUpgradeLog to Serilog and opens one child span per script.
/// </summary>
// ponytail: per-script spans are bracketed by parsing DbUp's "Executing ... script"
// log line — the only per-script hook DbUp exposes. If DbUp changes that wording,
// spans collapse to the root span (logs/metrics unaffected). Upgrade path: switch to
// a real per-script callback if DbUp ever adds one.
public sealed class SerilogUpgradeLog(ActivitySource source) : IUpgradeLog
{
    private Activity? _current;

    public void LogInformation(string format, params object[] args)
    {
        if (format.StartsWith("Executing", StringComparison.OrdinalIgnoreCase)
            && format.Contains("script", StringComparison.OrdinalIgnoreCase))
        {
            _current?.Dispose();
            var name = args.Length > 0 ? args[0]?.ToString() : "unknown";
            _current = source.StartActivity("migration.script");
            _current?.SetTag("script.name", name);
        }
        Write(Log.Information, format, args);
    }

    public void LogTrace(string format, params object[] args) => Write(Log.Verbose, format, args);
    public void LogDebug(string format, params object[] args) => Write(Log.Debug, format, args);
    public void LogWarning(string format, params object[] args) => Write(Log.Warning, format, args);

    public void LogError(string format, params object[] args)
    {
        _current?.SetStatus(ActivityStatusCode.Error);
        Write(Log.Error, format, args);
    }

    public void LogError(Exception ex, string format, params object[] args)
    {
        _current?.SetStatus(ActivityStatusCode.Error);
#pragma warning disable Serilog004
        Log.Error(ex, format, args);
#pragma warning restore Serilog004
    }

    private static void Write(Action<string, object[]> sink, string format, object[] args)
    {
#pragma warning disable Serilog004 // DbUp hands us runtime format strings, not constant templates
        sink(format, args);
#pragma warning restore Serilog004
    }
}
