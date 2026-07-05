using Prometheus;

namespace NomadRules.Ingest.Ingest;

// ponytail: best-effort telemetry (task 7.2). Wrapped so a broken metrics sink can never block an insert.
public static class IngestMetrics
{
    private static readonly Counter Ingested = Prometheus.Metrics.CreateCounter(
        "law_changes_ingested_total", "Messages processed by the ingest consumer, by result",
        new CounterConfiguration { LabelNames = ["result"] }); // inserted | duplicate | malformed | error

    public static void Record(string result)
    {
        try { Ingested.WithLabels(result).Inc(); }
        catch { /* a dead metrics sink never affects ingestion */ }
    }
}
