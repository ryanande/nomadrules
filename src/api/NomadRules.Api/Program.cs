using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using NomadRules.Api.Features.Auth;
using NomadRules.Api.Features.LawChanges;
using NomadRules.Api.Features.Marketing;
using NomadRules.Api.Features.Subscribers;
using NomadRules.Api.Features.Webhooks;
using NomadRules.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Db>();
builder.Services.AddScoped<SubscriberService>();
builder.Services.AddScoped<LawChangeService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MarketingService>();

// Entra External ID (CIAM) issues and signs subscriber tokens; the API only
// validates against its OIDC metadata (Authority) — no shared secret to manage
// or rotate. Portal attaches the token as `Authorization: Bearer` via MSAL.
var entraAuthority = builder.Configuration["Entra:Authority"]
    ?? throw new InvalidOperationException("Entra:Authority not configured");
var entraClientId = builder.Configuration["Entra:ClientId"]
    ?? throw new InvalidOperationException("Entra:ClientId not configured");
var entraTenantId = builder.Configuration["Entra:TenantId"]
    ?? throw new InvalidOperationException("Entra:TenantId not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = entraAuthority;
        o.Audience = entraClientId;
    });

builder.Services.AddAuthorization(o =>
{
    // Defense-in-depth: Authority/Audience already scope accepted tokens to the
    // CIAM tenant, but this makes the subscriber-vs-workforce split an explicit,
    // testable check rather than an implicit side effect of that configuration.
    o.AddPolicy("SubscriberTenant", p => p.RequireClaim("tid", entraTenantId));
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    // Global limiter: 100 req/min per client IP. Stripe webhooks are exempt
    // (Stripe has its own retry/backoff and is signature-verified).
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/webhooks"))
            return RateLimitPartition.GetNoLimiter("webhooks");

        var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddOpenApi();

// No AllowCredentials(): auth is a Bearer token (MSAL), not a cookie, so the
// Portal's fetches don't need `credentials: 'include'` and CORS doesn't need it either.
var portalOrigin = builder.Configuration["PortalOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(portalOrigin).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler(exApp => exApp.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new
    {
        error = "internal_error",
        message = "An unexpected error occurred"
    });
}));

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

SubscriberEndpoints.Map(app);
AuthEndpoints.Map(app);
MarketingEndpoints.Map(app);
StripeWebhookEndpoints.Map(app);

app.Run();
