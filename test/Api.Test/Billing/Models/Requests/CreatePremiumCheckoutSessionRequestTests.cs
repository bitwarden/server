using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Premium;
using Xunit;

namespace Bit.Api.Test.Billing.Models.Requests;

public class CreatePremiumCheckoutSessionRequestTests
{
    [Theory]
    [InlineData("ios")]
    [InlineData("android")]
    [InlineData("browser")]
    [InlineData("desktop")]
    public void Validate_SupportedPlatform_ReturnsNoErrors(string platform)
    {
        // Arrange
        var sut = new CreatePremiumCheckoutSessionRequest { Platform = platform };

        // Act
        var results = sut.Validate(new ValidationContext(sut)).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("unknown")]
    [InlineData("")]
    public void Validate_UnsupportedPlatform_ReturnsValidationError(string platform)
    {
        // Arrange
        var sut = new CreatePremiumCheckoutSessionRequest { Platform = platform };

        // Act
        var results = sut.Validate(new ValidationContext(sut)).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(nameof(CreatePremiumCheckoutSessionRequest.Platform), results[0].MemberNames);
    }
}
