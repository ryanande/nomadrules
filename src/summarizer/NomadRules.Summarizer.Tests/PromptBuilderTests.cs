using NomadRules.Summarizer.Summarization;

namespace NomadRules.Summarizer.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void Build_Tx_SubstitutesTexas()
        => Assert.Equal("in Texas, for Texas", PromptBuilder.Build("in {State}, for {State}", "TX"));

    [Fact]
    public void Build_Sd_SubstitutesSouthDakota()
        => Assert.Equal("South Dakota", PromptBuilder.Build("{State}", "SD"));

    [Fact]
    public void Build_LowercaseCode_Works()
        => Assert.Equal("South Dakota", PromptBuilder.Build("{State}", "sd"));

    [Fact]
    public void Build_UnknownCode_FallsBackToCode()
        => Assert.Equal("ZZ", PromptBuilder.Build("{State}", "ZZ"));

    [Fact]
    public void ShippedTemplate_HasPlaceholderAndNoHardcodedTexas()
    {
        var template = File.ReadAllText(ClaudeSummarizer.PromptPath);

        Assert.Contains("{State}", template);
        Assert.DoesNotContain("Texas", template);
    }
}
