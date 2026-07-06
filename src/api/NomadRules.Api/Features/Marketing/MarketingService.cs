using Dapper;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Features.Marketing;

public class MarketingService(Db db)
{
    public async Task CaptureLeadAsync(string email, string? source)
    {
        using var conn = db.Open();
        // Idempotent: a returning visitor re-submitting the same email is a no-op
        // rather than a unique-violation error (idx_leads_email is on lower(email)).
        await conn.ExecuteAsync(
            """
            INSERT INTO leads (email, source)
            VALUES (@email, @source)
            ON CONFLICT (lower(email)) DO NOTHING
            """,
            new { email = email.ToLowerInvariant(), source });
    }

    public async Task SaveContactAsync(string name, string email, string? topic, string message)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(
            """
            INSERT INTO contact_messages (name, email, topic, message)
            VALUES (@name, @email, @topic, @message)
            """,
            new { name, email = email.ToLowerInvariant(), topic, message });
    }
}
