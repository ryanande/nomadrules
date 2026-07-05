namespace NomadRules.Api.Features.Subscribers;

// Boundary validation for renewal month/day pairs. Keeps impossible dates (Feb 30, day-without-month) out of
// the DB so email-delivery's anchor (new DateOnly(year, month, day)) never throws. See renewal-date-entry
// design Decision 3.
public static class RenewalValidation
{
    // A leap year, so Feb 29 is accepted as a valid renewal day; the delivery anchor clamps it in non-leap years.
    private const int LeapYear = 2024;

    // Returns an error message, or null if all pairs are valid.
    public static string? Validate(IEnumerable<(int? Month, int? Day, string Name)> pairs)
    {
        foreach (var (month, day, name) in pairs)
        {
            if (month is < 1 or > 12)
                return $"{name} month must be 1-12";

            if (day is null) continue;

            if (month is null)
                return $"{name} day requires a month";

            var maxDay = DateTime.DaysInMonth(LeapYear, month.Value);
            if (day is < 1 || day > maxDay)
                return $"{name} day must be 1-{maxDay} for month {month}";
        }
        return null;
    }

    public static string? Validate(RegisterRequest r) => Validate(Pairs(
        r.InsuranceRenewalMonth, r.InsuranceRenewalDay,
        r.RegistrationRenewalMonth, r.RegistrationRenewalDay,
        r.LicenseRenewalMonth, r.LicenseRenewalDay,
        r.TaxDueMonth, r.TaxDueDay));

    public static string? Validate(UpdateProfileRequest r) => Validate(Pairs(
        r.InsuranceRenewalMonth, r.InsuranceRenewalDay,
        r.RegistrationRenewalMonth, r.RegistrationRenewalDay,
        r.LicenseRenewalMonth, r.LicenseRenewalDay,
        r.TaxDueMonth, r.TaxDueDay));

    private static IEnumerable<(int?, int?, string)> Pairs(
        int? im, int? id, int? rm, int? rd, int? lm, int? ld, int? tm, int? td) =>
    [
        (im, id, "Insurance"), (rm, rd, "Registration"), (lm, ld, "License"), (tm, td, "Tax"),
    ];
}
