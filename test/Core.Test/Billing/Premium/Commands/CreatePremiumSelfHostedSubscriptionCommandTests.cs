using System.Security.Claims;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class CreatePremiumSelfHostedSubscriptionCommandTests
{
    private readonly ILicensingService _licensingService = Substitute.For<ILicensingService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IPushNotificationService _pushNotificationService = Substitute.For<IPushNotificationService>();
    private readonly CreatePremiumSelfHostedSubscriptionCommand _command;

    public CreatePremiumSelfHostedSubscriptionCommandTests()
    {
        _command = new CreatePremiumSelfHostedSubscriptionCommand(
            _licensingService,
            _userService,
            _pushNotificationService,
            Substitute.For<ILogger<CreatePremiumSelfHostedSubscriptionCommand>>());
    }

    [Fact]
    public async Task Run_UserAlreadyPremium_ReturnsBadRequest()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = true
        };

        var license = new UserLicense
        {
            LicenseKey = "test_key",
            Expires = DateTime.UtcNow.AddYears(1)
        };

        // Act
        var result = await _command.Run(user, license);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Already a premium user.", badRequest.Response);
    }

    [Fact]
    public async Task Run_InvalidLicense_ReturnsBadRequest()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false
        };

        var license = new UserLicense
        {
            LicenseKey = "invalid_key",
            Expires = DateTime.UtcNow.AddYears(1)
        };

        _licensingService.VerifyLicense(license).Returns(false);

        // Act
        var result = await _command.Run(user, license);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Invalid license.", badRequest.Response);
    }

    [Fact]
    public async Task Run_LicenseCannotBeUsed_EmailNotVerified_ReturnsBadRequest()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false,
            Email = "test@example.com",
            EmailVerified = false
        };

        var license = new UserLicense
        {
            LicenseKey = "test_key",
            Expires = DateTime.UtcNow.AddYears(1),
            Token = "valid_token"
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("Email", "test@example.com")
        }));

        _licensingService.VerifyLicense(license).Returns(true);
        _licensingService.GetClaimsPrincipalFromLicense(license).Returns(claimsPrincipal);

        // Act
        var result = await _command.Run(user, license);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Contains("The user's email is not verified.", badRequest.Response);
    }

    [Fact]
    public async Task Run_LicenseCannotBeUsed_EmailMismatch_ReturnsBadRequest()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Premium = false,
            Email = "user@example.com",
            EmailVerified = true
        };

        var license = new UserLicense
        {
            LicenseKey = "test_key",
            Expires = DateTime.UtcNow.AddYears(1),
            Token = "valid_token"
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("Email", "license@example.com")
        }));

        _licensingService.VerifyLicense(license).Returns(true);
        _licensingService.GetClaimsPrincipalFromLicense(license).Returns(claimsPrincipal);

        // Act
        var result = await _command.Run(user, license);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Contains("The user's email does not match the license email.", badRequest.Response);
    }

    [Fact]
    public async Task Run_ValidRequest_Success()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Premium = false,
            Email = "test@example.com",
            EmailVerified = true
        };

        var license = new UserLicense
        {
            LicenseKey = "test_key_12345",
            Expires = DateTime.UtcNow.AddYears(1),
            Token = "valid_token"
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("Email", "test@example.com")
        }));

        _licensingService.VerifyLicense(license).Returns(true);
        _licensingService.GetClaimsPrincipalFromLicense(license).Returns(claimsPrincipal);

        // Act
        var result = await _command.Run(user, license);

        // Assert
        Assert.True(result.IsT0);

        // Verify user was updated correctly
        Assert.True(user.Premium);
        Assert.NotNull(user.LicenseKey);
        Assert.Equal(license.LicenseKey, user.LicenseKey);
        Assert.NotEqual(default, user.RevisionDate);

        // Verify services were called
        await _licensingService.Received(1).WriteUserLicenseAsync(user, license);
        await _userService.Received(1).SaveUserAsync(user);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(user.Id);
    }
}
