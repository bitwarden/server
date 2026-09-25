using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Models.Requests;
using Xunit;

namespace Bit.Subscriptions.Organization.Test.Models.Requests;

public class EnumMemberParameterTests
{
    [Theory]
    [InlineData("enterprise", PlanTierType.Enterprise)]
    [InlineData("Enterprise", PlanTierType.Enterprise)]
    [InlineData("families", PlanTierType.Families)]
    public void TryParse_EnumMemberValue_ReturnsTrueAndMapsToEnum(string value, PlanTierType expected)
    {
        var parsed = EnumMemberParameter<PlanTierType>.TryParse(value, null, out var result);

        Assert.True(parsed);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void TryParse_UnrecognizedValue_ReturnsFalse()
    {
        var parsed = EnumMemberParameter<PlanTierType>.TryParse("not-a-tier", null, out var result);

        Assert.False(parsed);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void TryParse_NumericOrdinal_ReturnsFalse()
    {
        var parsed = EnumMemberParameter<PlanCadenceType>.TryParse("0", null, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void Parse_UnrecognizedValue_ThrowsFormatException() =>
        Assert.Throws<FormatException>(() => EnumMemberParameter<PlanTierType>.Parse("not-a-tier", null));
}
