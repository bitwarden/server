using Bit.Core.Auth.Attributes;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Xunit;

namespace Bit.Core.Test.Auth.Attributes;

public class MarketingInitiativeValidationAttributeTests
{
    [Fact]
    public void IsValid_NullValue_ReturnsTrue()
    {
        var sut = new MarketingInitiativeValidationAttribute();

        var actual = sut.IsValid(null);

        Assert.True(actual);
    }

    [Theory]
    [InlineData(MarketingInitiativeConstants.Premium)]
    public void IsValid_AcceptedValue_ReturnsTrue(string value)
    {
        var sut = new MarketingInitiativeValidationAttribute();

        var actual = sut.IsValid(value);

        Assert.True(actual);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("Premium")]          // case sensitive - capitalized
    [InlineData("PREMIUM")]          // case sensitive - uppercase
    [InlineData("premium ")]         // trailing space
    [InlineData(" premium")]         // leading space
    public void IsValid_InvalidStringValue_ReturnsFalse(string value)
    {
        var sut = new MarketingInitiativeValidationAttribute();

        var actual = sut.IsValid(value);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(123)]                   // integer
    [InlineData(true)]                  // boolean
    [InlineData(45.67)]                 // double
    public void IsValid_NonStringValue_ReturnsFalse(object value)
    {
        var sut = new MarketingInitiativeValidationAttribute();

        var actual = sut.IsValid(value);

        Assert.False(actual);
    }

    [Fact]
    public void ErrorMessage_ContainsAcceptedValues()
    {
        var sut = new MarketingInitiativeValidationAttribute();

        var errorMessage = sut.ErrorMessage;

        Assert.NotNull(errorMessage);
        Assert.Contains("premium", errorMessage);
        Assert.Contains("Marketing initiative type must be one of:", errorMessage);
    }
}
