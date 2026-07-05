using Dapper;
using Microsoft.Data.Sqlite;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Features.Auth;

// Thrown when a token's email matches an existing subscriber but linking it
// would require trusting an unverified email claim (see ResolveOrProvisionAsync).
public class AccountLinkingConflictException(string message) : Exception(message);

// Entra issues and signs tokens; the API only validates them (see Program.cs's
// AddJwtBearer against the CIAM tenant's Authority). This service's only job is
// JIT-linking a validated token's oid claim to a subscribers row.
public class AuthService(Db db)
{
    public async Task<Subscribers.Subscriber> ResolveOrProvisionAsync(string entraOid, string email, bool emailVerified)
    {
        using var conn = db.Open();

        var byOid = await conn.QuerySingleOrDefaultAsync<Subscribers.Subscriber>(
            "SELECT * FROM subscribers WHERE entra_oid = @entraOid", new { entraOid });
        if (byOid is not null) return byOid;

        try
        {
            // The free renewal-calendar tool can create a subscribers row (email only,
            // no entra_oid) before the same person ever signs in — link it here instead
            // of creating a duplicate. Only auto-link when Entra has verified the email:
            // otherwise any token bearing a matching (unverified) email claim could take
            // over an existing subscriber's account.
            var byEmail = await conn.QuerySingleOrDefaultAsync<Subscribers.Subscriber>(
                "SELECT * FROM subscribers WHERE email = @email", new { email });
            if (byEmail is not null)
            {
                if (byEmail.EntraOid is not null || !emailVerified)
                {
                    throw new AccountLinkingConflictException(
                        "An account with this email already exists and could not be automatically linked.");
                }

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
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            // Lost a race to a concurrent first-login for the same oid/email — the other
            // request's insert/update already committed, so re-read and return its result
            // instead of surfacing the constraint violation as a 500.
            var winner = await conn.QuerySingleOrDefaultAsync<Subscribers.Subscriber>(
                "SELECT * FROM subscribers WHERE entra_oid = @entraOid OR email = @email",
                new { entraOid, email });
            if (winner is not null) return winner;
            throw;
        }
    }
}
