using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request;

public class TwoFactorDuoRequestModelValidationTests
{
    [Fact]
    public void ShouldReturnValidationError_WhenHostIsInvalid()
    {
        // Arrange
        var model = new UpdateTwoFactorDuoRequestModel
        {
            Host = "invalidHost",
            ClientId = "clientId",
            ClientSecret = "clientSecret",
        };

        // Act
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.Single(result);
        Assert.Equal("Host is invalid.", result.First().ErrorMessage);
        Assert.Equal("Host", result.First().MemberNames.First());
    }

    [Fact]
    public void ShouldReturnValidationError_WhenValuesAreInvalid()
    {
        // Arrange
        var model = new UpdateTwoFactorDuoRequestModel { Host = "api-12345abc.duosecurity.com" };

        // Act
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.Select(x => x.MemberNames.Contains("ClientId")).Any());
        Assert.True(result.Select(x => x.MemberNames.Contains("ClientSecret")).Any());
    }

    [Fact]
    public void ShouldReturnSuccess_WhenValuesAreValid()
    {
        // Arrange
        var model = new UpdateTwoFactorDuoRequestModel
        {
            Host = "api-12345abc.duosecurity.com",
            ClientId = "clientId",
            ClientSecret = "clientSecret",
        };

        // Act
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.Empty(result);
    }
}
