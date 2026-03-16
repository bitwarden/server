using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Portal;
using Xunit;

namespace Bit.Api.Test.Billing.Models.Requests.Portal;

public class PortalSessionRequestTests
{
    [Theory]
    [InlineData("https://example.com/return")]
    [InlineData("http://localhost:3000/billing")]
    [InlineData("https://app.bitwarden.com/settings/billing")]
    [InlineData("bitwarden://foo")]
    [InlineData("bitwarden://vault/item/12345")]
    public void Validate_ValidHttpsUrl_ReturnsNoErrors(string returnUrl)
    {
        // Arrange
        var request = new PortalSessionRequest { ReturnUrl = returnUrl };
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingReturnUrl_ReturnsRequiredError(string? returnUrl)
    {
        // Arrange
        var request = new PortalSessionRequest { ReturnUrl = returnUrl };
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PortalSessionRequest.ReturnUrl)));
    }

    [Theory]
    [InlineData("javascript:alert('xss')")]
    [InlineData("data:text/html,<script>alert('xss')</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/file")]
    public void Validate_NonHttpScheme_ReturnsSchemeError(string returnUrl)
    {
        // Arrange
        var request = new PortalSessionRequest { ReturnUrl = returnUrl };
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("HTTP, HTTPS, or bitwarden://"));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("://invalid")]
    [InlineData("http://")]
    public void Validate_InvalidUrl_ReturnsUrlFormatError(string returnUrl)
    {
        // Arrange
        var request = new PortalSessionRequest { ReturnUrl = returnUrl };
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void Validate_ExcessivelyLongUrl_ReturnsMaxLengthError()
    {
        // Arrange
        var longUrl = "https://example.com/" + new string('a', 2500);
        var request = new PortalSessionRequest { ReturnUrl = longUrl };
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PortalSessionRequest.ReturnUrl)));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Validate_WhitespaceOnlyUrl_ReturnsRequiredError(string returnUrl)
    {
        // Arrange
        var request = new PortalSessionRequest { ReturnUrl = returnUrl };
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(results);
    }
}
