using Dapper;
using NomadRules.Api.Features.Auth;
using NomadRules.Api.Features.Subscribers;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Tests;

public class SubscriberServiceStateTests : IDisposable
{
    private readonly Db _db = TestDb.Create();
    private readonly string _oid = Guid.NewGuid().ToString();
    private readonly string _email = $"state-{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task SetState_OnProvisionedSubscriber_StoresSd()
    {
        var provisioned = await new AuthService(_db).ResolveOrProvisionAsync(_oid, _email, emailVerified: true);
        var svc = new SubscriberService(_db);

        var updated = await svc.SetStateAsync(provisioned.Id, "SD");

        Assert.Equal("SD", updated.State);
        Assert.Equal("SD", (await svc.GetByIdAsync(provisioned.Id))!.State);
    }

    [Fact]
    public async Task SetState_CanOverwriteExistingState()
    {
        var svc = new SubscriberService(_db);
        var created = await svc.CreateAsync(new RegisterRequest(_email, "TX", null, null, null, null));

        var updated = await svc.SetStateAsync(created.Id, "SD");

        Assert.Equal("SD", updated.State);
    }

    public void Dispose()
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM subscribers WHERE entra_oid = @oid OR email = @email", new { oid = _oid, email = _email });
    }
}
