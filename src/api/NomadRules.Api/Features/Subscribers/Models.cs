namespace NomadRules.Api.Features.Subscribers;

public record RegisterRequest(
    string Email,
    string State,
    int? InsuranceRenewalMonth,
    int? RegistrationRenewalMonth,
    int? LicenseRenewalMonth,
    int? TaxDueMonth,
    int? InsuranceRenewalDay = null,
    int? RegistrationRenewalDay = null,
    int? LicenseRenewalDay = null,
    int? TaxDueDay = null
);

public record UpdateProfileRequest(
    int? InsuranceRenewalMonth,
    int? RegistrationRenewalMonth,
    int? LicenseRenewalMonth,
    int? TaxDueMonth,
    int? InsuranceRenewalDay = null,
    int? RegistrationRenewalDay = null,
    int? LicenseRenewalDay = null,
    int? TaxDueDay = null
);

public class Subscriber
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string State { get; set; } = default!;
    public int? InsuranceRenewalMonth { get; set; }
    public int? RegistrationRenewalMonth { get; set; }
    public int? LicenseRenewalMonth { get; set; }
    public int? TaxDueMonth { get; set; }
    public int? InsuranceRenewalDay { get; set; }
    public int? RegistrationRenewalDay { get; set; }
    public int? LicenseRenewalDay { get; set; }
    public int? TaxDueDay { get; set; }
    public string Tier { get; set; } = "free";
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? EntraOid { get; set; }
    public string CreatedAt { get; set; } = default!;
    public string UpdatedAt { get; set; } = default!;
}
