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
    /// Returns <see langword="true"/> if <paramref name="country"/> is in <see cref="DirectTaxCountries"/>,
    /// meaning tax is collected directly and Stripe's <c>tax_exempt</c> should default to <c>"none"</c>.
    /// Returns <see langword="false"/> for all other countries, where VAT reverse charge applies.
    /// </summary>
    public static bool IsDirectTaxCountry(string? country) =>
       country is not null and not "" && DirectTaxCountries.Contains(country);

    /// <summary>
    /// Returns the Stripe <c>tax_exempt</c> value appropriate for <paramref name="country"/>.<br/>
    /// If <paramref name="currentTaxExempt"/> is already <c>"exempt"</c>, that status is always preserved.<br/>
    /// For direct-tax countries, returns <c>"none"</c>.<br/>
    /// For all other countries, returns <c>"reverse"</c>.
    /// </summary>
    public static string DetermineTaxExemptStatus(string? country, string? currentTaxExempt = null) =>
        currentTaxExempt == TaxExempt.Exempt
            ? TaxExempt.Exempt
            : IsDirectTaxCountry(country) ? TaxExempt.None : TaxExempt.Reverse;
}
