using Microsoft.AspNetCore.Mvc;
using NomadRules.Api.Features.Subscribers;

namespace NomadRules.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        group.MapPost("/magic-link", SendMagicLink);
        group.MapGet("/verify", Verify);
    }

    private static async Task<IResult> SendMagicLink(
        [FromBody] MagicLinkRequest req,
        SubscriberService subSvc,
        AuthService authSvc,
        ILogger<AuthService> logger)
    {
        var sub = await subSvc.GetByEmailAsync(req.Email);
        // ponytail: always 200 to avoid email enumeration
        if (sub is null)
        {
            logger.LogInformation("Magic link requested for unknown email {Email}", req.Email);
            return Results.Ok(new { message = "If that email is registered, a link is on its way." });
        }

        var link = await authSvc.CreateMagicLinkAsync(sub.Id);
        logger.LogInformation("Magic link for {SubscriberId}: {Link}", sub.Id, link);
        // TODO: send via Resend — logging for now
        return Results.Ok(new { message = "If that email is registered, a link is on its way." });
    }

    private static async Task<IResult> Verify(
        [FromQuery] string token,
        AuthService authSvc)
    {
        var jwt = await authSvc.VerifyMagicLinkAsync(token);
        if (jwt is null)
            return Results.BadRequest(new { error = "invalid_token", message = "Token is invalid or expired" });

        return Results.Ok(new { token = jwt });
    }
}

public record MagicLinkRequest(string Email);
