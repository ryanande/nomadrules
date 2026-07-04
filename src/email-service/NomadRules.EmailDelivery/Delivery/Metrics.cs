using Prometheus;

namespace NomadRules.EmailDelivery.Delivery;

// ponytail: best-effort telemetry (design Decision 5 / task 7.3). Every record is wrapped so a broken
// metrics sink can never block or fail a send. Resend's free tier is 100/day — Sent(...) is how we watch it.
public static class DeliveryMetrics
{
    private static readonly Counter EmailsSent = Prometheus.Metrics.CreateCounter(
        "emails_sent_total", "Emails dispatched, by type and result",
        new CounterConfiguration { LabelNames = ["type", "result"] });

    public static void Sent(string type, bool ok)
    {
        try { EmailsSent.WithLabels(type, ok ? "success" : "failure").Inc(); }
        catch { /* a dead metrics sink never affects a send */ }
    }
}
