using NomadRules.Ingest.Ingest;

namespace NomadRules.Ingest.Workers;

// Drains the crawler's queue into law_changes. Each message: parse → insert (idempotent) → ack. A malformed
// message is dead-lettered and skipped; a DB failure leaves the message un-acked so it redelivers. The
// worker never crashes on a single bad message (design Decision 4).
public class IngestWorker(
    IMessageSource source,
    IngestRepository repo,
    IngestOptions options,
    ILogger<IngestWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Ingest worker started (transport {Transport})", options.Transport);
        while (!stoppingToken.IsCancellationRequested)
        {
            int handled;
            try
            {
                handled = await DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Ingest drain failed; backing off");
                handled = 0;
            }

            // Only idle when the queue is empty; otherwise loop straight back to drain the backlog.
            if (handled == 0)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    // One batch. Returns the number of messages settled (ack'd or dead-lettered). Entry point for --run-now.
    public async Task<int> DrainOnceAsync(CancellationToken ct)
    {
        var batch = await source.ReceiveAsync(options.BatchSize, ct);
        var settled = 0;

        foreach (var message in batch)
        {
            var msg = Mapping.TryParse(message.Raw);
            if (msg is null)
            {
                await source.DeadLetterAsync(message, "unparseable LawChangeDetected payload", ct);
                IngestMetrics.Record("malformed");
                settled++;
                continue;
            }

            try
            {
                var inserted = await repo.TryInsertAsync(Mapping.ToRow(msg, options.MvpDefaultState));
                // Ack only AFTER the row is committed. Duplicate (ignored) still acks — it's already durable.
                await source.CompleteAsync(message, ct);
                IngestMetrics.Record(inserted ? "inserted" : "duplicate");
                settled++;
                if (inserted)
                    log.LogInformation("Ingested {MessageId} from {SourceId} -> law_changes", msg.MessageId, msg.SourceId);
                else
                    log.LogDebug("Duplicate message {MessageId} ignored", msg.MessageId);
            }
            catch (Exception ex)
            {
                // DB failure: do NOT ack — leave the message for redelivery. Idempotent insert makes the retry safe.
                log.LogError(ex, "Failed to persist {MessageId}; leaving un-acked for redelivery", msg.MessageId);
                IngestMetrics.Record("error");
            }
        }
        return settled;
    }
}
