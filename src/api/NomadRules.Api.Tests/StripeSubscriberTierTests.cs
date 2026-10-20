using Dapper;
using NomadRules.Api.Features.Auth;
using NomadRules.Api.Features.Subscribers;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Tests;

public class StripeSubscriberTierTests : IDisposable
{
    private readonly Db _db = TestDb.Create();
    private readonly string _email = $"stripe-{Guid.NewGuid():N}@example.test";

    private async Task<SubscriberService> SeedAsync(string tier)
    {
        var svc = new SubscriberService(_db);
        await svc.CreateAsync(new RegisterRequest(_email, "TX", null, null, null, null));
        using var conn = _db.Open();
        await conn.ExecuteAsync("UPDATE subscribers SET tier = @tier WHERE email = @email", new { tier, email = _email });
        return svc;
    }

    private string TierOf()
    {
        using var conn = _db.Open();
        return conn.ExecuteScalar<string>("SELECT tier FROM subscribers WHERE email = @email", new { email = _email })!;
    }

    [Fact]
    public async Task SetStripeInfo_KnownTier_Overwrites()
    {
        var svc = await SeedAsync("free");
        await svc.SetStripeInfoAsync(_email, "cus_1", "sub_1", "pro");
        Assert.Equal("pro", TierOf());
    }

    [Fact]
    public async Task SetStripeInfo_UnknownTier_KeepsExistingPaidTier()
    {
        var svc = await SeedAsync("pro");
        await svc.SetStripeInfoAsync(_email, "cus_1", "sub_1", null);
        Assert.Equal("pro", TierOf());
    }

    [Fact]
    public async Task SetStripeInfo_UnknownTier_PromotesFreeToBasic()
    {
        var svc = await SeedAsync("free");
        await svc.SetStripeInfoAsync(_email, "cus_1", "sub_1", null);
        Assert.Equal("basic", TierOf());
    }

    [Fact]
    public async Task SetStripeInfo_UnknownTier_StillRecordsStripeIds()
    {
        var svc = await SeedAsync("pro");
        await svc.SetStripeInfoAsync(_email, "cus_9", "sub_9", null);
        using var conn = _db.Open();
        var ids = conn.QuerySingle<(string c, string s)>(
            "SELECT stripe_customer_id AS c, stripe_subscription_id AS s FROM subscribers WHERE email = @email", new { email = _email });
        Assert.Equal(("cus_9", "sub_9"), ids);
    }

    public void Dispose()
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM subscribers WHERE email = @email", new { email = _email });
    }
}
