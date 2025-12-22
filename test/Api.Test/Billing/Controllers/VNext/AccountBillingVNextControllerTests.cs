using Bit.Api.Billing.Controllers.VNext;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers.VNext;

public class AccountBillingVNextControllerTests
{
    private readonly ICreateBitPayInvoiceForCreditCommand _createBitPayInvoiceForCreditCommand;
    private readonly ICreatePremiumCloudHostedSubscriptionCommand _createPremiumCloudHostedSubscriptionCommand;
    private readonly IGetCreditQuery _getCreditQuery;
    private readonly IGetPaymentMethodQuery _getPaymentMethodQuery;
    private readonly IUpdatePaymentMethodCommand _updatePaymentMethodCommand;
    private readonly IUserService _userService;
    private readonly ILicensingService _licensingService;
    private readonly AccountBillingVNextController _sut;

    public AccountBillingVNextControllerTests()
    {
        _createBitPayInvoiceForCreditCommand = Substitute.For<ICreateBitPayInvoiceForCreditCommand>();
        _createPremiumCloudHostedSubscriptionCommand = Substitute.For<ICreatePremiumCloudHostedSubscriptionCommand>();
        _getCreditQuery = Substitute.For<IGetCreditQuery>();
        _getPaymentMethodQuery = Substitute.For<IGetPaymentMethodQuery>();
        _updatePaymentMethodCommand = Substitute.For<IUpdatePaymentMethodCommand>();
        _userService = Substitute.For<IUserService>();
        _licensingService = Substitute.For<ILicensingService>();

        _sut = new AccountBillingVNextController(
            _createBitPayInvoiceForCreditCommand,
            _createPremiumCloudHostedSubscriptionCommand,
            _getCreditQuery,
            _getPaymentMethodQuery,
            _updatePaymentMethodCommand,
            _userService,
            _licensingService);
    }

    [Fact]
    public async Task GetLicenseAsync_NullUser_ThrowsUnauthorizedAccessException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.GetLicenseAsync(null!));
    }

    [Theory, BitAutoData]
    public async Task GetLicenseAsync_ValidUser_ReturnsLicenseResponse(User user)
    {
        // Arrange
        var userLicense = new UserLicense
        {
            LicenseKey = "test-license-key",
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Premium = true,
            MaxStorageGb = 10,
            Version = 1,
            Issued = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddYears(1),
            Trial = false,
            LicenseType = Core.Enums.LicenseType.User
        };

        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal();

        _userService.GenerateLicenseAsync(user).Returns(userLicense);
        _licensingService.GetClaimsPrincipalFromLicense(userLicense).Returns(claimsPrincipal);

        // Act
        var result = await _sut.GetLicenseAsync(user);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _userService.Received(1).GenerateLicenseAsync(user);
        _licensingService.Received(1).GetClaimsPrincipalFromLicense(userLicense);
    }

    [Theory, BitAutoData]
    public async Task GetLicenseAsync_WithClaimsPrincipal_UsesTokenExpiration(User user)
    {
        // Arrange
        var tokenExpiration = DateTime.UtcNow.AddYears(1);
        var userLicense = new UserLicense
        {
            LicenseKey = "test-license-key",
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Premium = true,
            MaxStorageGb = 10,
            Version = 1,
            Issued = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMonths(6), // Different from token expiration
            Trial = false,
            LicenseType = Core.Enums.LicenseType.User,
            Token = "test-token"
        };

        var claims = new[]
        {
            new System.Security.Claims.Claim("Expires", tokenExpiration.ToString("O"))
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims);
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        _userService.GenerateLicenseAsync(user).Returns(userLicense);
        _licensingService.GetClaimsPrincipalFromLicense(userLicense).Returns(claimsPrincipal);

        // Act
        var result = await _sut.GetLicenseAsync(user);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _userService.Received(1).GenerateLicenseAsync(user);
        _licensingService.Received(1).GetClaimsPrincipalFromLicense(userLicense);
    }
}
