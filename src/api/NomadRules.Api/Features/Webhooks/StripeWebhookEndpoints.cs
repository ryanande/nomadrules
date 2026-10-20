using Stripe;
using NomadRules.Api.Features.Subscribers;

namespace NomadRules.Api.Features.Webhooks;

public static class StripeWebhookEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/webhooks/stripe", Handle)
           .AllowAnonymous()
           .WithTags("Webhooks")
           .DisableAntiforgery();
    }

    private static async Task<IResult> Handle(
        HttpRequest request,
        IConfiguration config,
        SubscriberService svc,
        ILogger<Event> logger)
    {
        var secret = config["Stripe:WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe:WebhookSecret not configured");

        string json;
        using (var reader = new StreamReader(request.Body))
            json = await reader.ReadToEndAsync();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json, request.Headers["Stripe-Signature"], secret);
        }
        catch (StripeException)
        {
            return Results.StatusCode(403);
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
            {
                var subscription = (Subscription)stripeEvent.Data.Object;
                var apiKey = config["Stripe:SecretKey"]
                    ?? throw new InvalidOperationException("Stripe:SecretKey not configured");
                var customer = await new CustomerService(new StripeClient(apiKey))
                    .GetAsync(subscription.CustomerId);
                var keys = subscription.Items.Data.Select(i => i.Price.LookupKey).ToList();
                var tier = StripeTiers.Resolve(subscription.Status, keys);
                if (tier != "free" && !keys.Any(k => StripeTiers.FromLookupKey(k) is not null))
                    logger.LogWarning(
                        "Subscription {SubscriptionId} has no recognized price lookup key ({Keys}); granting basic",
                        subscription.Id, string.Join(",", keys));
                await svc.SetStripeInfoAsync(
                    customer.Email!, subscription.CustomerId, subscription.Id, tier);
                break;
            }
            case EventTypes.CustomerSubscriptionDeleted:
            {
                var subscription = (Subscription)stripeEvent.Data.Object;
                await svc.SetTierAsync(subscription.Id, "free");
                break;
            }
            default:
                logger.LogInformation("Unhandled Stripe event: {Type}", stripeEvent.Type);
                break;
        }

        return Results.Ok();
    }
}
