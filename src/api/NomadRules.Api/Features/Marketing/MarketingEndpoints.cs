using Microsoft.AspNetCore.Mvc;

namespace NomadRules.Api.Features.Marketing;

// Public, anonymous funnel endpoints for the marketing site: email lead capture
// and the contact form. Per-IP rate limiting is applied by the global limiter in
// Program.cs; spam mitigation here is the honeypot check plus input validation.
public static class MarketingEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/leads", CaptureLead).AllowAnonymous().WithTags("Marketing");
        app.MapPost("/api/contact", SubmitContact).AllowAnonymous().WithTags("Marketing");
    }

    private static async Task<IResult> CaptureLead(
        [FromBody] LeadRequest req,
        MarketingService svc)
    {
        if (!IsValidEmail(req.Email))
            return Results.BadRequest(new { error = "invalid_email", message = "Valid email is required" });

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

        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { error = "invalid_name", message = "Name is required" });
        if (!IsValidEmail(req.Email))
            return Results.BadRequest(new { error = "invalid_email", message = "Valid email is required" });
        if (string.IsNullOrWhiteSpace(req.Message))
            return Results.BadRequest(new { error = "invalid_message", message = "Message is required" });

        await svc.SaveContactAsync(req.Name.Trim(), req.Email.Trim(), req.Topic, req.Message.Trim());
        return Results.Accepted();
    }

    // ponytail: good-enough email check; matches the SubscriberEndpoints bar. A
    // real deliverability check belongs downstream (Resend), not at this gate.
    private static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Length <= 320;
}

public record LeadRequest(string Email, string? Source);

// Website is the honeypot — named to look real to bots, always empty for humans.
public record ContactRequest(string Name, string Email, string? Topic, string Message, string? Website);
