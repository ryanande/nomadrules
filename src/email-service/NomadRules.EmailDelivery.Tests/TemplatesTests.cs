using NomadRules.EmailDelivery.Delivery;

namespace NomadRules.EmailDelivery.Tests;

public class TemplatesTests
{
    [Fact]
    public void UrgentAlert_ForFederalChange_SaysNationwideNotUs()
    {
        var change = new LawChangeRow("id", "Headline", "Summary", "urgent", "US");

        var (_, body) = Templates.UrgentAlert(change);

        Assert.Contains("nationwide", body);
        Assert.DoesNotContain("(US)", body);
    }

    [Fact]
    public void UrgentAlert_ForStateChange_NamesTheState()
    {
        var (_, body) = Templates.UrgentAlert(new LawChangeRow("id", "Headline", "Summary", "urgent", "SD"));

        Assert.Contains("your state (SD)", body);
    }
}
