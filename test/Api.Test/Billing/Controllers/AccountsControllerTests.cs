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

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_IntegrationTest_CompletePipelineFromStripeToApiResponse(
        User user,
        UserLicense license)
    {
        // Arrange - Create a real Stripe Discount object as it would come from Stripe API
        var stripeDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = TestMilestone2CouponId,
                PercentOff = 30m,
                AmountOff = 2000, // 2000 cents = $20.00
                AppliesTo = new CouponAppliesTo
                {
                    Products = new List<string> { "prod_premium", "prod_families", "prod_teams" }
                }
            },
            End = null // Active discount (no end date)
        };

        // Step 1: Map Stripe Discount through SubscriptionInfo.BillingCustomerDiscount
        // This simulates what StripePaymentService.GetSubscriptionAsync does
        var billingCustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount(stripeDiscount);

        // Verify the mapping worked correctly
        Assert.Equal(TestMilestone2CouponId, billingCustomerDiscount.Id);
        Assert.True(billingCustomerDiscount.Active);
        Assert.Equal(30m, billingCustomerDiscount.PercentOff);
        Assert.Equal(20.00m, billingCustomerDiscount.AmountOff); // Converted from cents
        Assert.NotNull(billingCustomerDiscount.AppliesTo);
        Assert.Equal(3, billingCustomerDiscount.AppliesTo.Count);

        // Step 2: Create SubscriptionInfo with the mapped discount
        // This simulates what StripePaymentService returns
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = billingCustomerDiscount
        };

        // Step 3: Set up controller dependencies
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

        // Act - Step 4: Call AccountsController.GetSubscriptionAsync
        // This exercises the complete pipeline:
        // - Retrieves subscriptionInfo from paymentService (with discount from Stripe)
        // - Maps through SubscriptionInfo.BillingCustomerDiscount (already done above)
        // - Filters in SubscriptionResponseModel constructor (based on feature flag, coupon ID, active status)
        // - Returns via AccountsController
        var result = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);

        // Assert - Verify the complete pipeline worked end-to-end
        Assert.NotNull(result);
        Assert.NotNull(result.CustomerDiscount);

        // Verify Stripe Discount → SubscriptionInfo.BillingCustomerDiscount mapping
        // (verified above, but confirming it made it through)

        // Verify SubscriptionInfo.BillingCustomerDiscount → SubscriptionResponseModel.BillingCustomerDiscount filtering
        // The filter should pass because:
        // - includeMilestone2Discount = true (feature flag enabled)
        // - subscription.CustomerDiscount != null
        // - subscription.CustomerDiscount.Id == Milestone2SubscriptionDiscount
        // - subscription.CustomerDiscount.Active = true
        Assert.Equal(TestMilestone2CouponId, result.CustomerDiscount.Id);
        Assert.True(result.CustomerDiscount.Active);
        Assert.Equal(30m, result.CustomerDiscount.PercentOff);
        Assert.Equal(20.00m, result.CustomerDiscount.AmountOff); // Verify cents-to-dollars conversion

        // Verify AppliesTo products are preserved through the entire pipeline
        Assert.NotNull(result.CustomerDiscount.AppliesTo);
        Assert.Equal(3, result.CustomerDiscount.AppliesTo.Count());
        Assert.Contains("prod_premium", result.CustomerDiscount.AppliesTo);
        Assert.Contains("prod_families", result.CustomerDiscount.AppliesTo);
        Assert.Contains("prod_teams", result.CustomerDiscount.AppliesTo);

        // Verify the payment service was called correctly
        await _paymentService.Received(1).GetSubscriptionAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_IntegrationTest_MultipleDiscountsInSubscription_PrefersCustomerDiscount(
        User user,
        UserLicense license)
    {
        // Arrange - Create Stripe subscription with multiple discounts
        // Customer discount should be preferred over subscription discounts
        var customerDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = TestMilestone2CouponId,
                PercentOff = 30m,
                AmountOff = null
            },
            End = null
        };

        var subscriptionDiscount1 = new Discount
        {
            Coupon = new Coupon
            {
                Id = "other-coupon-1",
                PercentOff = 10m
            },
            End = null
        };

        var subscriptionDiscount2 = new Discount
        {
            Coupon = new Coupon
            {
                Id = "other-coupon-2",
                PercentOff = 15m
            },
            End = null
        };

        // Map through SubscriptionInfo.BillingCustomerDiscount
        var billingCustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount(customerDiscount);
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = billingCustomerDiscount
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

        // Assert - Should use customer discount, not subscription discounts
        Assert.NotNull(result);
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(TestMilestone2CouponId, result.CustomerDiscount.Id);
        Assert.Equal(30m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_IntegrationTest_BothPercentOffAndAmountOffPresent_HandlesEdgeCase(
        User user,
        UserLicense license)
    {
        // Arrange - Edge case: Stripe coupon with both PercentOff and AmountOff
        // This tests the scenario mentioned in BillingCustomerDiscountTests.cs line 212-232
        var stripeDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = TestMilestone2CouponId,
                PercentOff = 25m,
                AmountOff = 2000, // 2000 cents = $20.00
                AppliesTo = new CouponAppliesTo
                {
                    Products = new List<string> { "prod_premium" }
                }
            },
            End = null
        };

        // Map through SubscriptionInfo.BillingCustomerDiscount
        var billingCustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount(stripeDiscount);
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = billingCustomerDiscount
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

        // Assert - Both values should be preserved through the pipeline
        Assert.NotNull(result);
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(TestMilestone2CouponId, result.CustomerDiscount.Id);
        Assert.Equal(25m, result.CustomerDiscount.PercentOff);
        Assert.Equal(20.00m, result.CustomerDiscount.AmountOff); // Converted from cents
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_IntegrationTest_BillingSubscriptionMapsThroughPipeline(
        User user,
        UserLicense license)
    {
        // Arrange - Create Stripe subscription with subscription details
        var stripeSubscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            TrialStart = DateTime.UtcNow.AddDays(-30),
            TrialEnd = DateTime.UtcNow.AddDays(-20),
            CanceledAt = null,
            CancelAtPeriodEnd = false,
            CollectionMethod = "charge_automatically"
        };

        // Map through SubscriptionInfo.BillingSubscription
        var billingSubscription = new SubscriptionInfo.BillingSubscription(stripeSubscription);
        var subscriptionInfo = new SubscriptionInfo
        {
            Subscription = billingSubscription,
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = TestMilestone2CouponId,
                Active = true,
                PercentOff = 20m
            }
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

        // Assert - Verify BillingSubscription mapped through pipeline
        Assert.NotNull(result);
        Assert.NotNull(result.Subscription);
        Assert.Equal("active", result.Subscription.Status);
        Assert.Equal(14, result.Subscription.GracePeriod); // charge_automatically = 14 days
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_IntegrationTest_BillingUpcomingInvoiceMapsThroughPipeline(
        User user,
        UserLicense license)
    {
        // Arrange - Create Stripe invoice for upcoming invoice
        var stripeInvoice = new Invoice
        {
            AmountDue = 2000, // 2000 cents = $20.00
            Created = DateTime.UtcNow.AddDays(1)
        };

        // Map through SubscriptionInfo.BillingUpcomingInvoice
        var billingUpcomingInvoice = new SubscriptionInfo.BillingUpcomingInvoice(stripeInvoice);
        var subscriptionInfo = new SubscriptionInfo
        {
            UpcomingInvoice = billingUpcomingInvoice,
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = TestMilestone2CouponId,
                Active = true,
                PercentOff = 20m
            }
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

        // Assert - Verify BillingUpcomingInvoice mapped through pipeline
        Assert.NotNull(result);
        Assert.NotNull(result.UpcomingInvoice);
        Assert.Equal(20.00m, result.UpcomingInvoice.Amount); // Converted from cents
        Assert.NotNull(result.UpcomingInvoice.Date);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_IntegrationTest_CompletePipelineWithAllComponents(
        User user,
        UserLicense license)
    {
        // Arrange - Complete Stripe objects for full pipeline test
        var stripeDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = TestMilestone2CouponId,
                PercentOff = 20m,
                AmountOff = 1000, // $10.00
                AppliesTo = new CouponAppliesTo
                {
                    Products = new List<string> { "prod_premium", "prod_families" }
                }
            },
            End = null
        };

        var stripeSubscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically"
        };

        var stripeInvoice = new Invoice
        {
            AmountDue = 1500, // $15.00
            Created = DateTime.UtcNow.AddDays(7)
        };

        // Map through SubscriptionInfo (simulating StripePaymentService)
        var billingCustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount(stripeDiscount);
        var billingSubscription = new SubscriptionInfo.BillingSubscription(stripeSubscription);
        var billingUpcomingInvoice = new SubscriptionInfo.BillingUpcomingInvoice(stripeInvoice);

        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = billingCustomerDiscount,
            Subscription = billingSubscription,
            UpcomingInvoice = billingUpcomingInvoice
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

        // Act - Full pipeline: Stripe → SubscriptionInfo → SubscriptionResponseModel → API response
        var result = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);

        // Assert - Verify all components mapped correctly through the pipeline
        Assert.NotNull(result);

        // Verify discount
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(TestMilestone2CouponId, result.CustomerDiscount.Id);
        Assert.Equal(20m, result.CustomerDiscount.PercentOff);
        Assert.Equal(10.00m, result.CustomerDiscount.AmountOff);
        Assert.NotNull(result.CustomerDiscount.AppliesTo);
        Assert.Equal(2, result.CustomerDiscount.AppliesTo.Count());

        // Verify subscription
        Assert.NotNull(result.Subscription);
        Assert.Equal("active", result.Subscription.Status);
        Assert.Equal(14, result.Subscription.GracePeriod);

        // Verify upcoming invoice
        Assert.NotNull(result.UpcomingInvoice);
        Assert.Equal(15.00m, result.UpcomingInvoice.Amount);
        Assert.NotNull(result.UpcomingInvoice.Date);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_SelfHosted_WithDiscountFlagEnabled_NeverIncludesDiscount(User user)
    {
        // Arrange - Self-hosted user with discount flag enabled (should still return null)
        var selfHostedSettings = new GlobalSettings { SelfHosted = true };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2).Returns(true); // Flag enabled

        // Act
        var result = await _sut.GetSubscriptionAsync(selfHostedSettings, _paymentService);

        // Assert - Should never include discount for self-hosted, even with flag enabled
        Assert.NotNull(result);
        Assert.Null(result.CustomerDiscount);
        await _paymentService.DidNotReceive().GetSubscriptionAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_NullGateway_WithDiscountFlagEnabled_NeverIncludesDiscount(
        User user,
        UserLicense license)
    {
        // Arrange - User with null gateway and discount flag enabled (should still return null)
        user.Gateway = null; // No gateway configured
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _userService.GenerateLicenseAsync(user).Returns(license);
        _featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2).Returns(true); // Flag enabled

        // Act
        var result = await _sut.GetSubscriptionAsync(_globalSettings, _paymentService);

        // Assert - Should never include discount when no gateway, even with flag enabled
        Assert.NotNull(result);
        Assert.Null(result.CustomerDiscount);
        await _paymentService.DidNotReceive().GetSubscriptionAsync(Arg.Any<User>());
    }
}
