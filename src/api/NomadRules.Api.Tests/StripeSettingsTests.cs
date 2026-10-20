using Microsoft.Extensions.Configuration;
using NomadRules.Api.Features.Webhooks;

namespace NomadRules.Api.Tests;

public class StripeSettingsTests
{
    private static IConfiguration Config(string? value) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Stripe:SecretKey"] = value })
        .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void RequireSecretKey_MissingOrBlank_ThrowsNamedError(string? value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => StripeSettings.RequireSecretKey(Config(value)));
        Assert.Contains("Stripe:SecretKey", ex.Message);
    }

    [Fact]
    public void RequireSecretKey_Present_ReturnsIt()
        => Assert.Equal("rk_test_abc", StripeSettings.RequireSecretKey(Config("rk_test_abc")));
}
