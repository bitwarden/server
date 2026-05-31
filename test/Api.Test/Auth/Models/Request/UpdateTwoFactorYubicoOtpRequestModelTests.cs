using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request;

public class UpdateTwoFactorYubicoOtpRequestModelTests
{
    [Fact]
    public void Validate_AllKeysEmpty_ReturnsValidationError()
    {
        // Arrange
        var model = new UpdateTwoFactorYubicoOtpRequestModel();

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("A key is required.", result.First().ErrorMessage);
        Assert.Equal("Key1", result.First().MemberNames.First());
    }

    [Theory]
    [InlineData("short", "Key 1 is invalid.", "Key1")]
    [InlineData("12345678901", "Key 1 is invalid.", "Key1")]
    public void Validate_KeyLengthLessThan12_ReturnsValidationError(string invalidKey, string expectedMessage, string expectedMemberName)
    {
        // Arrange
        var model = new UpdateTwoFactorYubicoOtpRequestModel { Key1 = invalidKey };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedMessage, result.First().ErrorMessage);
        Assert.Equal(expectedMemberName, result.First().MemberNames.First());
    }

    [Fact]
    public void Validate_MultipleInvalidKeys_ReturnsMultipleValidationErrors()
    {
        // Arrange
        var model = new UpdateTwoFactorYubicoOtpRequestModel
        {
            Key1 = "short",
            Key2 = "also_short"
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.ErrorMessage == "Key 1 is invalid.");
        Assert.Contains(result, r => r.ErrorMessage == "Key 2 is invalid.");
    }

    [Fact]
    public void Validate_ValidKey_ReturnsSuccess()
    {
        // Arrange
        var model = new UpdateTwoFactorYubicoOtpRequestModel { Key1 = "123456789012" };

        // Act
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.Empty(result);
    }
}
