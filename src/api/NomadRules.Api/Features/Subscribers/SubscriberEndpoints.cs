using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace NomadRules.Api.Features.Subscribers;

public static class SubscriberEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/subscribers").WithTags("Subscribers");

        group.MapPost("/", Register).AllowAnonymous();
        group.MapGet("/{id}/profile", GetProfile).RequireAuthorization();
        group.MapPut("/{id}/profile", UpdateProfile).RequireAuthorization();
        group.MapGet("/{id}/feed", GetFeed).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        [FromBody] RegisterRequest req,
        SubscriberService svc)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return Results.BadRequest(new { error = "invalid_email", message = "Valid email is required" });

        if (string.IsNullOrWhiteSpace(req.State))
            return Results.BadRequest(new { error = "invalid_state", message = "State is required" });

        foreach (var month in new[] { req.InsuranceRenewalMonth, req.RegistrationRenewalMonth,
                                       req.LicenseRenewalMonth, req.TaxDueMonth })
        {
            if (month is < 1 or > 12)
                return Results.BadRequest(new { error = "invalid_month", message = "Month must be 1-12" });
        }

        if (await svc.GetByEmailAsync(req.Email) is not null)
            return Results.Conflict(new { error = "duplicate_email", message = "Email already registered" });

        var sub = await svc.CreateAsync(req);
        return Results.Created($"/api/subscribers/{sub.Id}/profile", sub);
    }

    private static async Task<IResult> GetProfile(
        string id,
        ClaimsPrincipal user,
        SubscriberService svc)
    {
        if (!IsAuthorized(user, id)) return Results.Forbid();
        var sub = await svc.GetByIdAsync(id);
        return sub is null ? Results.NotFound() : Results.Ok(sub);
    }

    private static async Task<IResult> UpdateProfile(
        string id,
        [FromBody] UpdateProfileRequest req,
        ClaimsPrincipal user,
        SubscriberService svc)
    {
        if (!IsAuthorized(user, id)) return Results.Forbid();

        foreach (var month in new[] { req.InsuranceRenewalMonth, req.RegistrationRenewalMonth,
                                       req.LicenseRenewalMonth, req.TaxDueMonth })
        {
            if (month is < 1 or > 12)
                return Results.BadRequest(new { error = "invalid_month", message = "Month must be 1-12" });
        }

        if (await svc.GetByIdAsync(id) is null) return Results.NotFound();
        var updated = await svc.UpdateAsync(id, req);
        return Results.Ok(updated);
    }

    private static async Task<IResult> GetFeed(
        string id,
        ClaimsPrincipal user,
        LawChanges.LawChangeService lawSvc,
        SubscriberService subSvc,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0)
    {
        if (!IsAuthorized(user, id)) return Results.Forbid();

        var sub = await subSvc.GetByIdAsync(id);
        if (sub is null) return Results.NotFound();

        var (items, total) = await lawSvc.GetFeedAsync(sub.State, limit, offset);
        return Results.Ok(new { items, total_count = total, limit, offset });
    }

    // ponytail: simple claim check — no roles yet, add when admin endpoints land
    private static bool IsAuthorized(ClaimsPrincipal user, string id) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) == id;
}
