using System.Text;

namespace NomadRules.EmailDelivery.Delivery;

// Plain-text templates for v0.1 — Jenn owns copy/design polish later (design Non-Goals).
// Month-anchored dates say "around <Month>", never a false exact date (design Decision 1 trade-off).
public static class Templates
{
    private static readonly string[] MonthNames =
        ["", "January", "February", "March", "April", "May", "June",
         "July", "August", "September", "October", "November", "December"];

    public static (string Subject, string Body) RenewalAlert(string category, int offset, int renewalMonth)
    {
        var month = MonthNames[renewalMonth];
        var subject = $"Your {category} renewal is about {offset} days away";
        var body =
            $"""
            Heads up — your {category} renewal is coming up around {month}.

            You're getting this alert {offset} days ahead so you have time to review any
            recent law changes in your state before you renew.

            Log in to NomadRules to see what's changed and what to check before renewal.

            — NomadRules
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) UrgentAlert(LawChangeRow change)
    {
        var subject = $"Urgent: {change.Headline}";
        var body =
            $"""
            An urgent law change affects your state ({change.State}):

            {change.Headline}

            {change.Summary}

            Log in to NomadRules for the full details.

            — NomadRules
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) Digest(string state, IReadOnlyList<LawChangeRow> changes)
    {
        var subject = $"Your weekly NomadRules digest — {changes.Count} update(s) for {state}";
        var sb = new StringBuilder();
        sb.AppendLine($"Here's what changed this week in {state}:");
        sb.AppendLine();
        foreach (var c in changes)
        {
            sb.AppendLine($"• {c.Headline}");
            sb.AppendLine($"  {c.Summary}");
            sb.AppendLine();
        }
        sb.AppendLine("Log in to NomadRules for full details.");
        sb.AppendLine();
        sb.AppendLine("— NomadRules");
        return (subject, sb.ToString());
    }
}
