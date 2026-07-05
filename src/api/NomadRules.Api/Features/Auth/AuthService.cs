using Dapper;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Features.Auth;

// Entra issues and signs tokens; the API only validates them (see Program.cs's
// AddJwtBearer against the CIAM tenant's Authority). This service's only job is
// JIT-linking a validated token's oid claim to a subscribers row.
public class AuthService(Db db)
{
    public async Task<Subscribers.Subscriber> ResolveOrProvisionAsync(string entraOid, string email)
    {
        using var conn = db.Open();

        var byOid = await conn.QuerySingleOrDefaultAsync<Subscribers.Subscriber>(
            "SELECT * FROM subscribers WHERE entra_oid = @entraOid", new { entraOid });
        if (byOid is not null) return byOid;

        // The free renewal-calendar tool can create a subscribers row (email only,
        // no entra_oid) before the same person ever signs in — link it here instead
        // of creating a duplicate.
        var byEmail = await conn.QuerySingleOrDefaultAsync<Subscribers.Subscriber>(
            "SELECT * FROM subscribers WHERE email = @email", new { email });
        if (byEmail is not null)
        {
            await conn.ExecuteAsync(
                "UPDATE subscribers SET entra_oid = @entraOid, updated_at = datetime('now') WHERE id = @id",
                new { entraOid, id = byEmail.Id });
            byEmail.EntraOid = entraOid;
            return byEmail;
        }

        var id = Guid.NewGuid().ToString();
        await conn.ExecuteAsync("""
            INSERT INTO subscribers (id, email, state, tier, entra_oid, created_at, updated_at)
            VALUES (@id, @email, 'TX', 'free', @entraOid, datetime('now'), datetime('now'))
            """, new { id, email, entraOid });

        return (await conn.QuerySingleAsync<Subscribers.Subscriber>(
            "SELECT * FROM subscribers WHERE id = @id", new { id }))!;
    }
}
