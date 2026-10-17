namespace NomadRules.Summarizer.Summarization;

public static class PromptBuilder
{
    public static string Build(string template, string stateCode)
        => template.Replace("{State}", StateNames.FullName(stateCode));
}
