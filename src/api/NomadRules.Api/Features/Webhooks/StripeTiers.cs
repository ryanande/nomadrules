namespace NomadRules.Api.Features.Webhooks;

// Tier comes from the Stripe Price lookup key, "<tier>_<period>" (e.g. pro_monthly), which is
// identical in test and live mode, so no price IDs live in config.
public static class StripeTiers
{
    private static readonly string[] Ranked = ["basic", "pro", "business"];

    public static string Resolve(string? status, IEnumerable<string?> priceLookupKeys)
    {
        if (status is not ("active" or "trialing"))
            return "free";

        return priceLookupKeys
            .Select(FromLookupKey)
            .OfType<string>()
            .OrderByDescending(t => Array.IndexOf(Ranked, t))
            .FirstOrDefault() ?? Ranked[0];
    }

    public static string? FromLookupKey(string? key)
    {
        var i = key?.IndexOf('_') ?? -1;
        if (i <= 0) return null;
        var tier = key![..i].ToLowerInvariant();
        return Array.IndexOf(Ranked, tier) >= 0 ? tier : null;
    }
}
