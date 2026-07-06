using Microsoft.AspNetCore.Mvc;

namespace NomadRules.Api.Features.Marketing;

// Public, anonymous funnel endpoints for the marketing site: email lead capture
// and the contact form. Per-IP rate limiting is applied by the global limiter in
// Program.cs; spam mitigation here is the honeypot check plus input validation.
public static class MarketingEndpoints
{
    // Field caps: these are public anonymous write endpoints, so every stored
    // TEXT column gets an explicit length bound to prevent multi-MB blobs from
    // filling the DB (a 1MB body cap also backstops this in Program.cs).
    private const int MaxSource = 64;
    private const int MaxName = 120;
    private const int MaxTopic = 40;
    private const int MaxMessage = 4000;

    public static void Map(WebApplication app)
    {
        // Stricter, endpoint-specific limiter (see Program.cs "marketing" policy)
        // on top of the global limiter — these are spam-magnet public forms.
        app.MapPost("/api/leads", CaptureLead).AllowAnonymous().WithTags("Marketing")
            .RequireRateLimiting("marketing");
        app.MapPost("/api/contact", SubmitContact).AllowAnonymous().WithTags("Marketing")
            .RequireRateLimiting("marketing");
    }

    private static async Task<IResult> CaptureLead(
        [FromBody] LeadRequest req,
        MarketingService svc)
    {
        if (!IsValidEmail(req.Email))
            return Results.BadRequest(new { error = "invalid_email", message = "Valid email is required" });
        if (TooLong(req.Source, MaxSource))
            return Results.BadRequest(new { error = "invalid_source", message = "Source is too long" });

        await svc.CaptureLeadAsync(req.Email.Trim(), req.Source);
        // Idempotent on the client's side too — a repeat email is still a 202.
        return Results.Accepted();
    }

    private static async Task<IResult> SubmitContact(
        [FromBody] ContactRequest req,
        MarketingService svc)
    {
        // Honeypot: a hidden field real users never see. If it's filled, a bot
        // did it — accept (so the bot gets no signal) but drop it silently.
        if (!string.IsNullOrWhiteSpace(req.Website))
            return Results.Accepted();

        if (string.IsNullOrWhiteSpace(req.Name) || TooLong(req.Name, MaxName))
            return Results.BadRequest(new { error = "invalid_name", message = "Name is required (max 120 chars)" });
        if (!IsValidEmail(req.Email))
            return Results.BadRequest(new { error = "invalid_email", message = "Valid email is required" });
        if (TooLong(req.Topic, MaxTopic))
            return Results.BadRequest(new { error = "invalid_topic", message = "Topic is too long" });
        if (string.IsNullOrWhiteSpace(req.Message) || TooLong(req.Message, MaxMessage))
            return Results.BadRequest(new { error = "invalid_message", message = "Message is required (max 4000 chars)" });

        await svc.SaveContactAsync(req.Name.Trim(), req.Email.Trim(), req.Topic, req.Message.Trim());
        return Results.Accepted();
    }

    private static bool TooLong(string? value, int max) => value is not null && value.Length > max;

    // ponytail: good-enough email check; matches the SubscriberEndpoints bar. A
    // real deliverability check belongs downstream (Resend), not at this gate.
    private static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Length <= 320;
}

public record LeadRequest(string Email, string? Source);

// Website is the honeypot — named to look real to bots, always empty for humans.
public record ContactRequest(string Name, string Email, string? Topic, string Message, string? Website);
