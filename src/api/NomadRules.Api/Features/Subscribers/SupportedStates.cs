namespace NomadRules.Api.Features.Subscribers;

public static class SupportedStates
{
    public static readonly string[] Codes = ["TX", "SD"];

    public static bool IsSupported(string? state)
        => state is not null && Codes.Contains(state, StringComparer.OrdinalIgnoreCase);
}
