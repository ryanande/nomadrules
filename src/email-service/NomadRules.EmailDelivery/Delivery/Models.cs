namespace NomadRules.EmailDelivery.Delivery;

public class ResendOptions
{
    public string? ApiKey { get; set; }
    public string FromAddress { get; set; } = "NomadRules <alerts@nomadrules.com>";
    // Overridable only so local verification can point at a stub; defaults to the real Resend endpoint.
    public string Endpoint { get; set; } = "https://api.resend.com/emails";
}

public class DeliveryOptions
{
    public int TickIntervalHours { get; set; } = 24;
    public int DigestDayOfWeek { get; set; } = 5; // 0=Sunday..6=Saturday; 5=Friday
}

// A subscriber and the renewal month (1-12) + optional day (1-31) for one category. Month is null when unset
// for that category; day is null when only a month was entered (anchor falls back to the 1st).
// long? to match SQLite INTEGER (Dapper requires an exact type match).
public record SubscriberRow(string Id, string Email, string State, long? RenewalMonth, long? RenewalDay);

// A processed, review-passed law change eligible for delivery.
public record LawChangeRow(string Id, string Headline, string Summary, string Severity, string State);

// The four renewal categories and the subscribers month/day columns each maps to.
public static class Categories
{
    public static readonly IReadOnlyList<(string Name, string MonthColumn, string DayColumn)> All =
    [
        ("insurance", "insurance_renewal_month", "insurance_renewal_day"),
        ("registration", "registration_renewal_month", "registration_renewal_day"),
        ("license", "license_renewal_month", "license_renewal_day"),
        ("tax", "tax_due_month", "tax_due_day"),
    ];
}

public static class RenewalTriggers
{
    public static readonly int[] Offsets = [60, 30, 7];

    // Anchor a renewal to its month-and-day. A null day falls back to the 1st (the prior month-only
    // behavior — existing subscribers are unaffected). If the date has already passed this year, roll to the
    // same month/day next year. The day is clamped to the month's length per year, so a Feb-29 renewal
    // resolves to Feb 28 in non-leap years. See renewal-date-entry design Decision 2.
    public static DateOnly Anchor(int month, int? day, DateOnly today)
    {
        DateOnly Build(int year) => new(year, month, Math.Min(day ?? 1, DateTime.DaysInMonth(year, month)));
        var anchor = Build(today.Year);
        return anchor < today ? Build(today.Year + 1) : anchor;
    }

    // Returns 60/30/7 if `today` is exactly that many days before the anchor, else null.
    public static int? DueOffset(DateOnly anchor, DateOnly today)
    {
        var days = anchor.DayNumber - today.DayNumber;
        return Array.IndexOf(Offsets, days) >= 0 ? days : null;
    }
}

public static class IdempotencyKeys
{
    // Deterministic PK for renewal_alerts — the UNIQUE constraint dedups regardless, but a stable id
    // makes the reserve-then-send flow readable and retry-safe.
    public static string RenewalAlert(string subscriberId, string category, int offset, int year)
        => $"{subscriberId}:{category}:{offset}:{year}";

    // Deterministic PK for notifications — UNIQUE(subscriber, law_change) is the real guard.
    public static string Notification(string subscriberId, string lawChangeId)
        => $"{subscriberId}:{lawChangeId}";

    // Stable Resend Idempotency-Key for a digest: same subscriber + same set of change ids => same key,
    // so a crash-then-retry of the identical digest is deduped by Resend. Order-independent (sorted).
    public static string Digest(string subscriberId, IEnumerable<string> lawChangeIds)
        => $"digest:{subscriberId}:{string.Join(",", lawChangeIds.OrderBy(x => x, StringComparer.Ordinal))}";
}
