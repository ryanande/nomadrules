using NomadRules.Api.Features.Subscribers;

namespace NomadRules.Api.Tests;

public class SupportedStatesTests
{
    [Theory]
    [InlineData("TX")]
    [InlineData("SD")]
    [InlineData("sd")]
    [InlineData("Tx")]
    public void IsSupported_AcceptsAllowListedCodesCaseInsensitively(string state)
        => Assert.True(SupportedStates.IsSupported(state));

    [Theory]
    [InlineData("CA")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void IsSupported_RejectsUnknownOrBlank(string? state)
        => Assert.False(SupportedStates.IsSupported(state));

    [Fact]
    public void Codes_AreExactlyTxAndSd()
        => Assert.Equal(new[] { "TX", "SD" }, SupportedStates.Codes);
}
