using System.ComponentModel.DataAnnotations;
using Bit.Api.Vault.Models;
using Bit.Core.Vault.Enums;
using Xunit;

namespace Bit.Api.Test.Vault.Models;

public class CipherFieldModelTests
{
    /// <summary>
    /// Tests that plain text in the Name field is rejected by validation.
    /// This is a regression test for the DoS vulnerability where a user could
    /// submit plain text instead of encrypted data, causing decryption failures
    /// that broke the vault for all organization members.
    /// </summary>
    [Theory]
    [InlineData("Test")] // Plain text - should be rejected
    [InlineData("Hello World")] // Plain text - should be rejected
    [InlineData("")] // Empty string - should be rejected
    [InlineData("not-encrypted-at-all")] // Plain text - should be rejected
    [InlineData("invalid|format")] // Invalid format - should be rejected
    public void Validate_PlainTextName_ReturnsValidationError(string plainTextName)
    {
        var model = new CipherFieldModel
        {
            Type = FieldType.Text,
            Name = plainTextName,
            Value = "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==" // Valid encrypted value
        };

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        Assert.False(isValid, $"Plain text '{plainTextName}' should have been rejected by validation");
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(CipherFieldModel.Name)));
    }

    /// <summary>
    /// Tests that plain text in the Value field is rejected by validation.
    /// </summary>
    [Theory]
    [InlineData("Test")] // Plain text - should be rejected
    [InlineData("SecretPassword123")] // Plain text - should be rejected
    [InlineData("")] // Empty string - should be rejected
    public void Validate_PlainTextValue_ReturnsValidationError(string plainTextValue)
    {
        var model = new CipherFieldModel
        {
            Type = FieldType.Text,
            Name = "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==", // Valid encrypted name
            Value = plainTextValue
        };

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        Assert.False(isValid, $"Plain text value '{plainTextValue}' should have been rejected by validation");
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(CipherFieldModel.Value)));
    }

    /// <summary>
    /// Tests that properly encrypted strings in Name and Value pass validation.
    /// </summary>
    [Theory]
    [InlineData("2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // AesCbc256_HmacSha256_B64
    [InlineData("0.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==")] // AesCbc256_B64
    [InlineData("aXY=|Y3Q=|cnNhQ3Q=")] // Legacy format without header
    public void Validate_EncryptedStrings_PassesValidation(string encryptedString)
    {
        var model = new CipherFieldModel
        {
            Type = FieldType.Text,
            Name = encryptedString,
            Value = encryptedString
        };

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        Assert.True(isValid, $"Encrypted string '{encryptedString}' should have passed validation. Errors: {string.Join(", ", validationResults.Select(r => r.ErrorMessage))}");
    }

    /// <summary>
    /// Tests that null values are allowed (fields are optional).
    /// </summary>
    [Fact]
    public void Validate_NullNameAndValue_PassesValidation()
    {
        var model = new CipherFieldModel
        {
            Type = FieldType.Text,
            Name = null,
            Value = null
        };

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        Assert.True(isValid, $"Null values should be allowed. Errors: {string.Join(", ", validationResults.Select(r => r.ErrorMessage))}");
    }
}
