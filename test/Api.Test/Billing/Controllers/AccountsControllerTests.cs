using System.Security.Claims;
using Bit.Api.Billing.Controllers;
using Bit.Core;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers;

[SubscriptionInfoCustomize]
public class AccountsControllerTests : IDisposable
{
    private readonly IUserService _userService;
    private readonly IFeatureService _featureService;
    private readonly IPaymentService _paymentService;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IUserAccountKeysQuery _userAccountKeysQuery;
    private readonly GlobalSettings _globalSettings;
    private readonly AccountsController _sut;

    public AccountsControllerTests()
    {
        _userService = Substitute.For<IUserService>();
        _featureService = Substitute.For<IFeatureService>();
        _paymentService = Substitute.For<IPaymentService>();
        _twoFactorIsEnabledQuery = Substitute.For<ITwoFactorIsEnabledQuery>();
        _userAccountKeysQuery = Substitute.For<IUserAccountKeysQuery>();
        _globalSettings = new GlobalSettings { SelfHosted = false };

        _sut = new AccountsController(
            _userService,
            _twoFactorIsEnabledQuery,
            _userAccountKeysQuery,
            _featureService
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WhenFeatureFlagEnabled_IncludesDiscount(
        User user,
        SubscriptionInfo subscriptionInfo,
        UserLicense license)
    {
        // Arrange
        subscriptionInfo.CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
        {
            Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
            Active = true,
            PercentOff = 20m,
            AmountOff = null,
            AppliesTo = new List<string> { "product1" }
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2).Returns(true);
        _paymentService.GetSubscriptionAsync(user).Returns(subscriptionInfo);
        _userService.GenerateLicenseAsync(user, subscriptionInfo).Returns(license);

        user.Gateway = GatewayType.Stripe; // User has payment gateway

        // Act
        var result = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(20m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WhenFeatureFlagDisabled_ExcludesDiscount(
        User user,
        SubscriptionInfo subscriptionInfo,
        UserLicense license)
    {
        // Arrange
        subscriptionInfo.CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
        {
            Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
            Active = true,
            PercentOff = 20m,
            AmountOff = null,
            AppliesTo = new List<string> { "product1" }
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2).Returns(false);
        _paymentService.GetSubscriptionAsync(user).Returns(subscriptionInfo);
        _userService.GenerateLicenseAsync(user, subscriptionInfo).Returns(license);

        user.Gateway = GatewayType.Stripe; // User has payment gateway

        // Act
        var result = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.CustomerDiscount); // Should be null when feature flag is disabled
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithNonMatchingCouponId_ExcludesDiscount(
        User user,
        SubscriptionInfo subscriptionInfo,
        UserLicense license)
    {
        // Arrange
        subscriptionInfo.CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
        {
            Id = "different-coupon-id", // Non-matching coupon ID
            Active = true,
            PercentOff = 20m,
            AmountOff = null,
            AppliesTo = new List<string> { "product1" }
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2).Returns(true);
        _paymentService.GetSubscriptionAsync(user).Returns(subscriptionInfo);
        _userService.GenerateLicenseAsync(user, subscriptionInfo).Returns(license);

        user.Gateway = GatewayType.Stripe; // User has payment gateway

        // Act
        var result = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.CustomerDiscount); // Should be null when coupon ID doesn't match
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WhenSelfHosted_ReturnsBasicResponse(User user)
    {
        // Arrange
        var selfHostedSettings = new GlobalSettings { SelfHosted = true };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);

        // Act
        var result = await _sut.GetSubscriptionAsync(selfHostedSettings, _paymentService);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.CustomerDiscount);
        await _paymentService.DidNotReceive().GetSubscriptionAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WhenNoGateway_ExcludesDiscount(User user, UserLicense license)
    {
        // Arrange
        user.Gateway = null; // No gateway configured
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _userService.GenerateLicenseAsync(user).Returns(license);

        // Act
        var result = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.CustomerDiscount); // Should be null when no gateway
        await _paymentService.DidNotReceive().GetSubscriptionAsync(Arg.Any<User>());
    }
}
