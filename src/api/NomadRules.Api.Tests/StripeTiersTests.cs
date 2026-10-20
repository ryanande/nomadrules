using NomadRules.Api.Features.Webhooks;

namespace NomadRules.Api.Tests;

public class StripeTiersTests
{
    [Theory]
    [InlineData("active", "basic_monthly", "basic")]
    [InlineData("active", "pro_annual", "pro")]
    [InlineData("active", "business_monthly", "business")]
    [InlineData("trialing", "pro_monthly", "pro")]
    [InlineData("active", "PRO_Monthly", "pro")]
    public void Resolve_PaidStatus_UsesTierFromPriceLookupKey(string status, string key, string expected)
        => Assert.Equal(expected, StripeTiers.Resolve(status, new[] { key }));

    [Theory]
    [InlineData("canceled")]
    [InlineData("past_due")]
    [InlineData("unpaid")]
    [InlineData("incomplete")]
    [InlineData("incomplete_expired")]
    [InlineData("paused")]
    [InlineData(null)]
    public void Resolve_NonPaidStatus_IsFree(string? status)
        => Assert.Equal("free", StripeTiers.Resolve(status, new[] { "pro_monthly" }));

    [Fact]
    public void Resolve_MultipleItems_PicksHighestTier()
        => Assert.Equal("business", StripeTiers.Resolve("active", new[] { "basic_monthly", "business_annual", "pro_monthly" }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gold_monthly")]
    [InlineData("pro")]
    public void Resolve_PaidButUnrecognizedKey_FallsBackToBasic(string? key)
        => Assert.Equal("basic", StripeTiers.Resolve("active", new[] { key }));

    [Fact]
    public void Resolve_PaidWithNoItems_FallsBackToBasic()
        => Assert.Equal("basic", StripeTiers.Resolve("active", Array.Empty<string?>()));

    [Theory]
    [InlineData("pro_monthly", "pro")]
    [InlineData("basic_annual", "basic")]
    [InlineData("gold_monthly", null)]
    [InlineData("pro", null)]
    [InlineData(null, null)]
    public void FromLookupKey_ParsesTierPrefix(string? key, string? expected)
        => Assert.Equal(expected, StripeTiers.FromLookupKey(key));
}
