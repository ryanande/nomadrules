using Dapper;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Features.Subscribers;

public class SubscriberService(Db db)
{
    public async Task<Subscriber?> GetByIdAsync(string id)
    {
        using var conn = db.Open();
        return await conn.QuerySingleOrDefaultAsync<Subscriber>(
            "SELECT * FROM subscribers WHERE id = @id", new { id });
    }

    public async Task<Subscriber?> GetByEmailAsync(string email)
    {
        using var conn = db.Open();
        return await conn.QuerySingleOrDefaultAsync<Subscriber>(
            "SELECT * FROM subscribers WHERE email = @email", new { email });
    }

    public async Task<Subscriber?> GetByEntraOidAsync(string entraOid)
    {
        using var conn = db.Open();
        return await conn.QuerySingleOrDefaultAsync<Subscriber>(
            "SELECT * FROM subscribers WHERE entra_oid = @entraOid", new { entraOid });
    }

    public async Task<Subscriber> CreateAsync(RegisterRequest req)
    {
        var sub = new Subscriber
        {
            Id = Guid.NewGuid().ToString(),
            Email = req.Email.ToLowerInvariant(),
            State = req.State.ToUpper(),
            InsuranceRenewalMonth = req.InsuranceRenewalMonth,
            RegistrationRenewalMonth = req.RegistrationRenewalMonth,
            LicenseRenewalMonth = req.LicenseRenewalMonth,
            TaxDueMonth = req.TaxDueMonth,
        };

        using var conn = db.Open();
        await conn.ExecuteAsync("""
            INSERT INTO subscribers
              (id, email, state, insurance_renewal_month, registration_renewal_month,
               license_renewal_month, tax_due_month, tier, created_at, updated_at)
            VALUES
              (@Id, @Email, @State, @InsuranceRenewalMonth, @RegistrationRenewalMonth,
               @LicenseRenewalMonth, @TaxDueMonth, 'free', datetime('now'), datetime('now'))
            """, sub);
        // Re-read so the response carries DB-set timestamps (created_at/updated_at).
        return (await GetByIdAsync(sub.Id))!;
    }

    public async Task<Subscriber> UpdateAsync(string id, UpdateProfileRequest req)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("""
            UPDATE subscribers SET
              insurance_renewal_month     = COALESCE(@InsuranceRenewalMonth, insurance_renewal_month),
              registration_renewal_month  = COALESCE(@RegistrationRenewalMonth, registration_renewal_month),
              license_renewal_month       = COALESCE(@LicenseRenewalMonth, license_renewal_month),
              tax_due_month               = COALESCE(@TaxDueMonth, tax_due_month),
              updated_at                  = datetime('now')
            WHERE id = @Id
            """, new { Id = id, req.InsuranceRenewalMonth, req.RegistrationRenewalMonth,
                       req.LicenseRenewalMonth, req.TaxDueMonth });

        return (await GetByIdAsync(id))!;
    }

    public async Task SetStripeInfoAsync(string email, string customerId, string subscriptionId, string tier)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("""
            UPDATE subscribers SET
              stripe_customer_id     = @customerId,
              stripe_subscription_id = @subscriptionId,
              tier                   = @tier,
              updated_at             = datetime('now')
            WHERE email = @email
            """, new { email, customerId, subscriptionId, tier });
    }

    public async Task SetTierAsync(string subscriptionId, string tier)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("""
            UPDATE subscribers SET tier = @tier, updated_at = datetime('now')
            WHERE stripe_subscription_id = @subscriptionId
            """, new { subscriptionId, tier });
    }
}
