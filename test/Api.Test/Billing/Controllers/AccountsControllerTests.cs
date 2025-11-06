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
using Stripe;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers;

[SubscriptionInfoCustomize]
public class AccountsControllerTests : IDisposable
{
    private const string TestMilestone2CouponId = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount;

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
            Id = TestMilestone2CouponId,
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
            Id = TestMilestone2CouponId,
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

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithInactiveDiscount_ExcludesDiscount(
        User user,
        SubscriptionInfo subscriptionInfo,
        UserLicense license)
    {
        // Arrange
        subscriptionInfo.CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
        {
            Id = TestMilestone2CouponId,
            Active = false, // Inactive discount
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
        Assert.Null(result.CustomerDiscount); // Should be null when discount is inactive
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_FullPipeline_ConvertsStripeDiscountToApiResponse(
        User user,
        UserLicense license)
    {
        // Arrange - Create a Stripe Discount object with real structure
        var stripeDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = TestMilestone2CouponId,
                PercentOff = 25m,
                AmountOff = 1400, // 1400 cents = $14.00
                AppliesTo = new CouponAppliesTo
                {
                    Products = new List<string> { "prod_premium", "prod_families" }
                }
            },
            End = null // Active discount
        };

        // Convert Stripe Discount to BillingCustomerDiscount (simulating what StripePaymentService does)
        var billingDiscount = new SubscriptionInfo.BillingCustomerDiscount(stripeDiscount);

        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = billingDiscount
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

        user.Gateway = GatewayType.Stripe;

        // Act
        var result = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);

        // Assert - Verify full pipeline conversion
        Assert.NotNull(result);
        Assert.NotNull(result.CustomerDiscount);

        // Verify Stripe data correctly converted to API response
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.True(result.CustomerDiscount.Active);
        Assert.Equal(25m, result.CustomerDiscount.PercentOff);

        // Verify cents-to-dollars conversion (1400 cents -> $14.00)
        Assert.Equal(14.00m, result.CustomerDiscount.AmountOff);

        // Verify AppliesTo products are preserved
        Assert.NotNull(result.CustomerDiscount.AppliesTo);
        Assert.Equal(2, result.CustomerDiscount.AppliesTo.Count());
        Assert.Contains("prod_premium", result.CustomerDiscount.AppliesTo);
        Assert.Contains("prod_families", result.CustomerDiscount.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_FullPipeline_WithFeatureFlagToggle_ControlsVisibility(
        User user,
        UserLicense license)
    {
        // Arrange - Create Stripe Discount
        var stripeDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = TestMilestone2CouponId,
                PercentOff = 20m
            },
            End = null
        };

        var billingDiscount = new SubscriptionInfo.BillingCustomerDiscount(stripeDiscount);
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = billingDiscount
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _paymentService.GetSubscriptionAsync(user).Returns(subscriptionInfo);
        _userService.GenerateLicenseAsync(user, subscriptionInfo).Returns(license);
        user.Gateway = GatewayType.Stripe;

        // Act & Assert - Feature flag ENABLED
        _featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2).Returns(true);
        var resultWithFlag = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);
        Assert.NotNull(resultWithFlag.CustomerDiscount);

        // Act & Assert - Feature flag DISABLED
        _featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2).Returns(false);
        var resultWithoutFlag = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);
        Assert.Null(resultWithoutFlag.CustomerDiscount);
    }
}
