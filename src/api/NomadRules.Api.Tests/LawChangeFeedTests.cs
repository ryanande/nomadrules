using NomadRules.Api.Features.LawChanges;

namespace NomadRules.Api.Tests;

public class LawChangeFeedTests
{
    [Fact]
    public async Task GetFeed_WithNullState_ReturnsEmptyNotError()
    {
        var (items, total) = await new LawChangeService(TestDb.Create()).GetFeedAsync(null, 20, 0);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }
}
