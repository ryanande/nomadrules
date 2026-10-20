namespace NomadRules.Api.Features.Webhooks;

public static class StripeSettings
{
    public static string RequireSecretKey(IConfiguration config)
    {
        var key = config["Stripe:SecretKey"];
        return string.IsNullOrWhiteSpace(key)
            ? throw new InvalidOperationException("Stripe:SecretKey not configured")
            : key;
    }
}
