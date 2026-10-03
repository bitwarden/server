using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request;

public class UpdateTwoFactorYubicoOtpRequestModelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("            ")]
    public void Validate_NullOrWhiteSpaceKeys_ReturnsKeyRequiredError(string? emptyValue)
    {
        var model = new UpdateTwoFactorYubicoOtpRequestModel
        {
            Key1 = emptyValue,
            Key2 = emptyValue,
            Key3 = emptyValue,
            Key4 = emptyValue,
            Key5 = emptyValue
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Equal("A key is required.", result.First().ErrorMessage);
        Assert.Equal("Key1", result.First().MemberNames.First());
    }

    [Theory]
    [InlineData("a", "Key 1 is invalid.", "Key1")]
    [InlineData("12345678901", "Key 1 is invalid.", "Key1")]
    public void Validate_KeyLengthLessThan12_ReturnsValidationError(string invalidKey, string expectedMessage, string expectedMemberName)
    {
        var model = new UpdateTwoFactorYubicoOtpRequestModel { Key1 = invalidKey };
        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Equal(expectedMessage, result.First().ErrorMessage);
        Assert.Equal(expectedMemberName, result.First().MemberNames.First());
    }

    [Fact]
    public void Validate_MultipleInvalidKeys_ReturnsMultipleValidationErrors()
    {
        var model = new UpdateTwoFactorYubicoOtpRequestModel
        {
            Key1 = "a",
            Key2 = "ab"
        };
        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.ErrorMessage == "Key 1 is invalid.");
        Assert.Contains(result, r => r.ErrorMessage == "Key 2 is invalid.");
    }

    [Fact]
    public void Validate_ValidKey_ReturnsSuccess()
    {
        var model = new UpdateTwoFactorYubicoOtpRequestModel { Key1 = "123456789012" };
        var result = model.Validate(new ValidationContext(model));

        Assert.Empty(result);
    }
}
