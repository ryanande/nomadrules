using NomadRules.EmailDelivery.Delivery;

namespace NomadRules.EmailDelivery.Workers;

// One polling worker (design Decision 4). Each tick: due renewal alerts, then urgent law changes
// (immediate), then — on the digest day — the weekly digest.
//
// Idempotency has two layers: DB reserve rows (INSERT-then-check) guard sequential retries, and a stable
// Resend Idempotency-Key guards what the DB can't — two OS processes racing past the reserve, or a crash
// between a successful send and the mark. The provider key is what makes concurrent/retried execution
// actually safe from duplicate delivery; the DB rows keep our own state consistent.
public class DeliveryWorker(
    DeliveryRepository repo,
    ResendClient resend,
    DeliveryOptions options,
    ILogger<DeliveryWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Delivery worker started (tick {Hours}h, digest day {Day})",
            options.TickIntervalHours, options.DigestDayOfWeek);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Delivery tick failed; backing off before the next tick");
            }

            try { await Task.Delay(TimeSpan.FromHours(options.TickIntervalHours), stoppingToken); }
            catch (OperationCanceledException) { /* shutting down */ }
        }
    }

    // One full pass. Also the entry point for `--run-now` (single pass) and local testing.
    public async Task RunOnceAsync(CancellationToken ct)
    {
        var today = Today();
        await SendRenewalAlertsAsync(today, ct);
        await SendUrgentAlertsAsync(ct);

        if ((int)today.DayOfWeek == options.DigestDayOfWeek)
            await SendDigestsAsync(ct);
        else
            log.LogInformation("Not the digest day ({Today} != {Digest}); skipping digest pass",
                (int)today.DayOfWeek, options.DigestDayOfWeek);
    }

    // Real clock in prod; NOMADRULES_TODAY (yyyy-MM-dd) overrides it for deterministic local verification.
    // A malformed override falls back to the real clock with a warning rather than crashing the tick.
    private DateOnly Today()
    {
        var raw = Environment.GetEnvironmentVariable("NOMADRULES_TODAY");
        if (string.IsNullOrEmpty(raw))
            return DateOnly.FromDateTime(DateTime.UtcNow);
        if (DateOnly.TryParse(raw, out var overridden))
            return overridden;
        log.LogWarning("NOMADRULES_TODAY='{Raw}' is not a valid yyyy-MM-dd date; using the real clock", raw);
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private async Task SendRenewalAlertsAsync(DateOnly today, CancellationToken ct)
    {
        foreach (var (category, monthColumn, dayColumn) in Categories.All)
        {
            var subs = await repo.SubscribersWithRenewalAsync(monthColumn, dayColumn);
            foreach (var sub in subs)
            {
                var month = (int)sub.RenewalMonth!.Value;
                var day = (int?)sub.RenewalDay;
                var anchor = RenewalTriggers.Anchor(month, day, today);
                var offset = RenewalTriggers.DueOffset(anchor, today);
                if (offset is null) continue;

                var id = IdempotencyKeys.RenewalAlert(sub.Id, category, offset.Value, anchor.Year);

                // Reserve before send; proceed only if a send is still owed (new or prior failure).
                if (!await repo.TryReserveRenewalAlertAsync(id, sub.Id, category, offset.Value, anchor.Year))
                    continue;

                var (subject, body) = Templates.RenewalAlert(category, offset.Value, month, day);
                var ok = await resend.SendAsync(sub.Email, subject, body, $"renewal:{id}", ct);
                DeliveryMetrics.Sent("renewal", ok);
                if (ok)
                {
                    await repo.MarkRenewalAlertSentAsync(id);
                    log.LogInformation("Renewal alert sent: {Sub} {Category} {Offset}d", sub.Id, category, offset);
                }
                else
                {
                    log.LogError("Renewal alert send failed: {Sub} {Category} {Offset}d — will retry next tick",
                        sub.Id, category, offset);
                }
            }
        }
    }

    private async Task SendUrgentAlertsAsync(CancellationToken ct)
    {
        // Urgent changes bypass the digest — one immediate email per matching change, to every subscriber
        // in the affected state (not just those with a renewal month).
        var subs = await repo.AllSubscribersAsync();
        foreach (var sub in subs)
        {
            var urgent = await repo.UnsentLawChangesAsync(sub.Id, sub.State, urgentOnly: true);
            foreach (var change in urgent)
            {
                var id = IdempotencyKeys.Notification(sub.Id, change.Id);
                if (!await repo.TryReserveNotificationAsync(id, sub.Id, change.Id, "urgent"))
                    continue;

                var (subject, body) = Templates.UrgentAlert(change);
                var ok = await resend.SendAsync(sub.Email, subject, body, $"urgent:{id}", ct);
                DeliveryMetrics.Sent("urgent", ok);
                if (ok)
                {
                    await repo.MarkNotificationSentAsync(id);
                    log.LogInformation("Urgent alert sent: {Sub} {Change}", sub.Id, change.Id);
                }
                else
                {
                    log.LogError("Urgent alert send failed: {Sub} {Change} — will retry next tick", sub.Id, change.Id);
                }
            }
        }
    }

    private async Task SendDigestsAsync(CancellationToken ct)
    {
        var subs = await repo.AllSubscribersAsync();
        foreach (var sub in subs)
        {
            var changes = await repo.UnsentLawChangesAsync(sub.Id, sub.State, urgentOnly: false);
            if (changes.Count == 0) continue; // no empty digests (design Risks)

            // Reserve every included change BEFORE sending (symmetric with renewal/urgent). A crash after
            // the send but before marking leaves these rows unsent, so the unsent query re-selects them next
            // tick and the identical digest is re-sent under the same Idempotency-Key — Resend dedups it.
            foreach (var change in changes)
                await repo.TryReserveNotificationAsync(
                    IdempotencyKeys.Notification(sub.Id, change.Id), sub.Id, change.Id, "digest");

            var (subject, body) = Templates.Digest(sub.State, changes);
            var key = IdempotencyKeys.Digest(sub.Id, changes.Select(c => c.Id));
            var ok = await resend.SendAsync(sub.Email, subject, body, key, ct);
            DeliveryMetrics.Sent("digest", ok);
            if (!ok)
            {
                log.LogError("Digest send failed: {Sub} ({Count} items) — reserved rows stay unsent, retries next tick",
                    sub.Id, changes.Count);
                continue;
            }

            foreach (var change in changes)
                await repo.MarkNotificationSentAsync(IdempotencyKeys.Notification(sub.Id, change.Id));
            log.LogInformation("Digest sent: {Sub} ({Count} items)", sub.Id, changes.Count);
        }
    }
}
