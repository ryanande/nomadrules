using Dapper;
using NomadRules.Api.Features.Auth;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly Db _db = TestDb.Create();
    private readonly string _oid = Guid.NewGuid().ToString();
    private readonly string _email = $"auth-{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task NewSubscriber_IsProvisionedWithNullState()
    {
        var sub = await new AuthService(_db).ResolveOrProvisionAsync(_oid, _email, emailVerified: true);

        Assert.Null(sub.State);
        Assert.Equal("free", sub.Tier);
    }

    [Fact]
    public async Task ExistingSubscriber_KeepsItsState()
    {
        using (var conn = _db.Open())
        {
            await conn.ExecuteAsync(
                "INSERT INTO subscribers (id, email, state, tier, entra_oid) VALUES (@id, @email, 'TX', 'free', @oid)",
                new { id = Guid.NewGuid().ToString(), email = _email, oid = _oid });
        }

        var sub = await new AuthService(_db).ResolveOrProvisionAsync(_oid, _email, emailVerified: true);

        Assert.Equal("TX", sub.State);
    }

    public void Dispose()
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM subscribers WHERE entra_oid = @oid OR email = @email", new { oid = _oid, email = _email });
    }
}
