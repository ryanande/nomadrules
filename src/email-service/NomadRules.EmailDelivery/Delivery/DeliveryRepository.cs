using Dapper;
using NomadRules.EmailDelivery.Infrastructure;

namespace NomadRules.EmailDelivery.Delivery;

// ponytail: no row locking — v0.1 runs a single worker/CronJob. Cross-process duplicate delivery is
// guarded by the Resend Idempotency-Key at the provider, not by app memory or DB row locks.
public class DeliveryRepository(Db db)
{
    private static string Now() => DateTime.UtcNow.ToString("o");

    // The only renewal columns we ever interpolate. Guards the raw-SQL build below against any future caller
    // passing an unvetted column name.
    private static readonly HashSet<string> AllowedRenewalColumns =
        Categories.All.SelectMany(c => new[] { c.MonthColumn, c.DayColumn }).ToHashSet();

    // Subscribers with a non-null renewal month for the given category, with their optional renewal day.
    // Both columns come from the fixed Categories list — interpolated into SQL, so they are allowlisted
    // rather than trusted.
    public async Task<IReadOnlyList<SubscriberRow>> SubscribersWithRenewalAsync(string monthColumn, string dayColumn)
    {
        if (!AllowedRenewalColumns.Contains(monthColumn) || !AllowedRenewalColumns.Contains(dayColumn))
            throw new ArgumentException($"Unknown renewal column '{monthColumn}'/'{dayColumn}'");

        using var conn = db.Open();
        var rows = await conn.QueryAsync<SubscriberRow>($"""
            SELECT id AS Id, email AS Email, state AS State, {monthColumn} AS RenewalMonth, {dayColumn} AS RenewalDay
            FROM subscribers
            WHERE {monthColumn} IS NOT NULL
            """);
        return rows.AsList();
    }

    // All subscribers, for state-matched digest/urgent delivery. Returns only contact fields — no renewal
    // columns, so nothing here can be misread as a subscriber's renewal month/day.
    public async Task<IReadOnlyList<SubscriberContact>> AllSubscribersAsync()
    {
        using var conn = db.Open();
        var rows = await conn.QueryAsync<SubscriberContact>(
            "SELECT id AS Id, email AS Email, state AS State FROM subscribers");
        return rows.AsList();
    }

    // Reserve the alert row, then report whether a send is still owed (sent_at IS NULL). True when we just
    // created the row OR a prior attempt reserved it but failed to send — so failed sends retry next tick.
    // A completed row (sent_at set) returns false: that's the idempotency guarantee. Single-instance v0.1
    // accepts the bounded crash-window re-send (design Risks).
    public async Task<bool> TryReserveRenewalAlertAsync(string id, string subscriberId, string category, int offset, int year)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("""
            INSERT OR IGNORE INTO renewal_alerts (id, subscriber_id, category, trigger_offset, renewal_year, sent_at)
            VALUES (@id, @subscriberId, @category, @offset, @year, NULL)
            """, new { id, subscriberId, category, offset, year });
        return await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM renewal_alerts WHERE id = @id AND sent_at IS NULL", new { id }) > 0;
    }

    public async Task MarkRenewalAlertSentAsync(string id)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("UPDATE renewal_alerts SET sent_at = @now WHERE id = @id", new { now = Now(), id });
    }

    // Processed, review-approved law changes for a subscriber's state that haven't been sent to them yet.
    // Raw content is never selected — only headline/summary reach the email.
    public async Task<IReadOnlyList<LawChangeRow>> UnsentLawChangesAsync(string subscriberId, string state, bool urgentOnly)
    {
        using var conn = db.Open();
        var severityFilter = urgentOnly ? "AND severity = 'urgent'" : "";
        var rows = await conn.QueryAsync<LawChangeRow>($"""
            SELECT id AS Id, headline AS Headline, summary AS Summary, severity AS Severity, state AS State
            FROM law_changes
            WHERE processed_at IS NOT NULL
              AND reviewed = 1
              AND state = @state
              {severityFilter}
              AND id NOT IN (
                SELECT law_change_id FROM notifications
                WHERE subscriber_id = @subscriberId AND sent_at IS NOT NULL)
            ORDER BY detected_at
            """, new { subscriberId, state });
        return rows.AsList();
    }

    // Reserve a notification row, then report whether a send is still owed (sent_at IS NULL) — same
    // reserve-then-retry semantics as renewal alerts.
    public async Task<bool> TryReserveNotificationAsync(string id, string subscriberId, string lawChangeId, string deliveryType)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("""
            INSERT OR IGNORE INTO notifications (id, subscriber_id, law_change_id, delivery_type, sent_at)
            VALUES (@id, @subscriberId, @lawChangeId, @deliveryType, NULL)
            """, new { id, subscriberId, lawChangeId, deliveryType });
        return await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM notifications WHERE id = @id AND sent_at IS NULL", new { id }) > 0;
    }

    public async Task MarkNotificationSentAsync(string id)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("UPDATE notifications SET sent_at = @now WHERE id = @id", new { now = Now(), id });
    }
}
