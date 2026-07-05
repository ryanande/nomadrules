using System.Security.Claims;

namespace NomadRules.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth").RequireAuthorization("SubscriberTenant");

        // First call after an Entra sign-in: JIT-resolves (or provisions) the
        // subscriber for this token so the Portal learns its internal subscriber id.
        // No server-side logout — MSAL clears its own cache and, optionally,
        // redirects through Entra's end-session endpoint client-side.
        group.MapGet("/me", Me);
    }

    private static async Task<IResult> Me(ClaimsPrincipal user, AuthService authSvc)
    {
        var oid = user.FindFirstValue("oid");
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("emails");
        if (oid is null || email is null)
            return Results.BadRequest(new { error = "invalid_token", message = "Token is missing oid/email claims" });

        var emailVerified = bool.TryParse(user.FindFirstValue("email_verified"), out var v) && v;

        try
        {
            var sub = await authSvc.ResolveOrProvisionAsync(oid, email, emailVerified);
            return Results.Ok(sub);
        }
        catch (AccountLinkingConflictException ex)
        {
            return Results.Conflict(new { error = "account_linking_conflict", message = ex.Message });
        }
    }
}
