using Bit.Core.Billing.Tax.Utilities;
using Xunit;
using CountryAbbreviations = Bit.Core.Constants.CountryAbbreviations;
using TaxExempt = Bit.Core.Billing.Constants.StripeConstants.TaxExempt;

namespace Bit.Core.Test.Billing.Tax;

public class TaxHelpersTests
{

    [Theory]
    [InlineData(CountryAbbreviations.UnitedStates, true)]
    [InlineData(CountryAbbreviations.Switzerland, true)]
    [InlineData("DE", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsDirectTaxCountry_ReturnsExpectedResult(string? country, bool expected)
    {
        var result = TaxHelpers.IsDirectTaxCountry(country);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("DE", TaxExempt.None, TaxExempt.Reverse)]                              // non-direct-tax → Reverse
    [InlineData(CountryAbbreviations.UnitedStates, TaxExempt.Reverse, TaxExempt.None)] // US Reverse → None (direct-tax)
    [InlineData(CountryAbbreviations.Switzerland, null, TaxExempt.None)]               // CH no existing status → None
    [InlineData(CountryAbbreviations.UnitedStates, TaxExempt.None, TaxExempt.None)]   // US already None → None
    [InlineData(CountryAbbreviations.Switzerland, TaxExempt.Reverse, TaxExempt.None)] // CH Reverse → None (direct-tax, not preserved)
    [InlineData("DE", TaxExempt.Reverse, TaxExempt.Reverse)]                          // non-direct-tax already Reverse → Reverse
    [InlineData(null, TaxExempt.None, TaxExempt.Reverse)]                             // unknown country → Reverse
    [InlineData("DE", TaxExempt.Exempt, TaxExempt.Exempt)]                            // exempt always preserved — non-direct-tax country
    [InlineData(CountryAbbreviations.UnitedStates, TaxExempt.Exempt, TaxExempt.Exempt)] // exempt always preserved — direct-tax country
    [InlineData(CountryAbbreviations.Switzerland, TaxExempt.Exempt, TaxExempt.Exempt)]  // exempt always preserved — CH
    public void DetermineTaxExemptStatus_ReturnsExpectedResult(
        string? country,
        string? currentTaxExempt,
        string expected)
    {
        var result = TaxHelpers.DetermineTaxExemptStatus(country, currentTaxExempt);
        Assert.Equal(expected, result);
    }
}
