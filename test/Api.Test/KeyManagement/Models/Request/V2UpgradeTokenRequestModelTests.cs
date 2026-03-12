using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Models.Requests;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Models.Request;

public class V2UpgradeTokenRequestModelTests
{
    private const string _validWrappedKey1 = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private const string _validWrappedKey2 = "2.BPt52Ie9PQjDQYkzKLDjEB==|P7PIiu3V3iKHCTOHojnKnh==|jE44t9C79D9KiZZiTb5W2uBskwMs9fFbHrPW8CSp6Kl=";

    [Fact]
    public void Validate_WithValidEncStrings_ReturnsNoErrors()
    {
        // Arrange
        var model = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = _validWrappedKey1,
            WrappedUserKey2 = _validWrappedKey2
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithMissingWrappedUserKey1_ReturnsValidationError()
    {
        // Arrange
        var model = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = null!,
            WrappedUserKey2 = _validWrappedKey2
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains("WrappedUserKey1"));
    }

    [Fact]
    public void Validate_WithMissingWrappedUserKey2_ReturnsValidationError()
    {
        // Arrange
        var model = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = _validWrappedKey1,
            WrappedUserKey2 = null!
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains("WrappedUserKey2"));
    }

    [Fact]
    public void Validate_WithInvalidEncStringFormatKey1_ReturnsValidationError()
    {
        // Arrange
        var model = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = "not-an-encrypted-string",
            WrappedUserKey2 = _validWrappedKey2
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "WrappedUserKey1 is not a valid encrypted string.");
    }

    [Fact]
    public void Validate_WithInvalidEncStringFormatKey2_ReturnsValidationError()
    {
        // Arrange
        var model = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = _validWrappedKey1,
            WrappedUserKey2 = "not-an-encrypted-string"
        };

        // Act
        var results = Validate(model);

        // Assert
        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "WrappedUserKey2 is not a valid encrypted string.");
    }

    [Fact]
    public void ToData_WithValidModel_MapsPropertiesCorrectly()
    {
        // Arrange
        var model = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = _validWrappedKey1,
            WrappedUserKey2 = _validWrappedKey2
        };

        // Act
        var data = model.ToData();

        // Assert
        Assert.Equal(_validWrappedKey1, data.WrappedUserKey1);
        Assert.Equal(_validWrappedKey2, data.WrappedUserKey2);
    }

    private static List<ValidationResult> Validate(V2UpgradeTokenRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
