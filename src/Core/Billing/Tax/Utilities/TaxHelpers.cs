using CountryAbbreviations = Bit.Core.Constants.CountryAbbreviations;
using TaxExempt = Bit.Core.Billing.Constants.StripeConstants.TaxExempt;
namespace Bit.Core.Billing.Tax.Utilities;

public static class TaxHelpers
{
    /// <summary>
    /// Countries where tax is collected directly from customers, rather than through VAT ID reverse charge.
    /// To add a new country, add its ISO 3166 code to <see cref="Bit.Core.Constants.CountryAbbreviations"/>
    /// and then add it to this set.
    /// </summary>
    private static readonly HashSet<string> DirectTaxCountries =
    [
        CountryAbbreviations.UnitedStates,
        CountryAbbreviations.Switzerland
    ];

    /// <summary>
    /// For countries where tax is collected directly, we generally want to default Stripe's tax_exempt to "none".
    /// However, some customer may have been manually set up with "reverse" tax_exempt status, so we want to preserve that manual override for those customers.
    /// This set defines the countries for which we should preserve that manual override.
    /// to add a new country, add its ISO 3166 code to <see cref="Bit.Core.Constants.CountryAbbreviations"/>
    /// </summary>
    private static readonly HashSet<string> PreserveReverseChargeCountries =
        [ CountryAbbreviations.Switzerland ];

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="country"/> is in <see cref="DirectTaxCountries"/>,
    /// meaning tax is collected directly and Stripe's <c>tax_exempt</c> should default to <c>"none"</c>.
    /// Returns <see langword="false"/> for all other countries, where VAT reverse charge applies.
    /// </summary>
    public static bool IsDirectTaxCountry(string? country) =>
       country is not null and not "" && DirectTaxCountries.Contains(country);

    /// <summary>
    /// Returns the Stripe <c>tax_exempt</c> value appropriate for <paramref name="country"/>.<br/>
    /// For non-direct-tax countries, always returns <c>"reverse"</c>.<br/>
    /// For direct-tax countries, returns <c>"none"</c> — unless the country is in <see cref="PreserveReverseChargeCountries"/> and
    /// <paramref name="currentTaxExempt"/> is already <c>"reverse"</c>
    /// </summary>
    public static string DetermineTaxExemptStatus(string? country, string? currentTaxExempt = null) =>
        !IsDirectTaxCountry(country)
            ? TaxExempt.Reverse
            : IsManualReverseChargeOverridden(country, currentTaxExempt)
                ? TaxExempt.Reverse
                : TaxExempt.None;

    /// <summary>
    /// Returns <see langword="true"/> if the current tax exempt status should be retained for the given country.
    /// </summary>
    private static bool IsManualReverseChargeOverridden(string? country, string? taxExemptStatus) =>
        country is not null and not ""
        && taxExemptStatus is not null and not "" and TaxExempt.Reverse
        && PreserveReverseChargeCountries.Contains(country);

}
