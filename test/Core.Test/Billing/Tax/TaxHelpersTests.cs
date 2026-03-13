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
    [InlineData(CountryAbbreviations.UnitedStates, TaxExempt.Reverse, TaxExempt.None)] // US manual Reverse → None
    [InlineData(CountryAbbreviations.Switzerland, null, TaxExempt.None)]               // CH no existing status → None
    [InlineData(CountryAbbreviations.UnitedStates, TaxExempt.None, TaxExempt.None)]   // US already None → None
    [InlineData(CountryAbbreviations.Switzerland, TaxExempt.Reverse, TaxExempt.Reverse)] // CH manual Reverse → preserved
    [InlineData("DE", TaxExempt.Reverse, TaxExempt.Reverse)]                           // non-direct-tax already Reverse → Reverse
    [InlineData(null, TaxExempt.None, TaxExempt.Reverse)]                              // unknown country → Reverse
    public void DetermineTaxExemptStatus_ReturnsExpectedResult(
        string? country,
        string? currentTaxExempt,
        string expected)
    {
        var result = TaxHelpers.DetermineTaxExemptStatus(country, currentTaxExempt);
        Assert.Equal(expected, result);
    }
}
