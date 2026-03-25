using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Stripe.Checkout;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;


namespace Bit.Core.Test.Billing.Premium.Commands;

public class CreatePremiumCheckoutSessionCommandTests
{
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly IGlobalSettings _globalSettings = Substitute.For<IGlobalSettings>();
    private readonly ILogger<CreatePremiumCheckoutSessionCommand> _logger = Substitute.For<ILogger<CreatePremiumCheckoutSessionCommand>>();
    private readonly ICreatePremiumCheckoutSessionCommand _command;

    private const string _successUrl = "success/url";
    private const string _cancelUrl = "cancel/url";

    public CreatePremiumCheckoutSessionCommandTests()
    {
        var stripeSettings = new GlobalSettings.StripeSettings
        {
            PremiumCheckoutSuccessUrl = _successUrl,
            PremiumCheckoutCancelUrl = _cancelUrl
        };
        _globalSettings.Stripe.Returns(stripeSettings);

        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new PremiumPurchasable { Price = 10M, StripePriceId = StripeConstants.Prices.PremiumAnnually },
            Storage = new PremiumPurchasable { Price = 4M, StripePriceId = StripeConstants.Prices.StoragePlanPersonal }
        };

        _pricingClient.GetAvailablePremiumPlan().Returns(premiumPlan);

        _command = new CreatePremiumCheckoutSessionCommand(
            _stripeAdapter,
            _pricingClient,
            _subscriberService,
            _globalSettings,
            _logger);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_UserNotPremium_UserDoesNotHaveExistingStripeCustomer_ReturnsCheckoutSessionUrl(User user)
    {
        // Arrange
        user.Premium = false;
        user.Name = "Test User";
        user.Email = "test@example.com";
        user.GatewayCustomerId = null;
        const string appVersion = "1.0.0";
        const string platform = "iOS";

        const string checkoutSessionUrl = "https://checkout.stripe.com/session/123";
        _stripeAdapter.CreateCheckoutSessionAsync(Arg.Any<SessionCreateOptions>()).Returns(new Session { Url = checkoutSessionUrl });
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(new Customer { Id = "cus_123" });

        // Act
        var result = await _command.Run(user, appVersion, platform);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(checkoutSessionUrl, result.AsT0.CheckoutSessionUrl);
        await _stripeAdapter.Received(1).CreateCheckoutSessionAsync(Arg.Is<SessionCreateOptions>(options =>
            options.Customer == user.GatewayCustomerId
            && options.Mode == StripeConstants.CheckoutSession.Modes.Subscription
            && options.LineItems[0].Price == StripeConstants.Prices.PremiumAnnually
            && options.LineItems[0].Quantity == 1
            && options.AutomaticTax.Enabled == true
            && options.SuccessUrl == _successUrl
            && options.CancelUrl == _cancelUrl
            && options.SubscriptionData.Metadata[StripeConstants.MetadataKeys.UserId] == user.Id.ToString()
            && options.SubscriptionData.Metadata[StripeConstants.MetadataKeys.OriginatingAppVersion] == appVersion
            && options.SubscriptionData.Metadata[StripeConstants.MetadataKeys.OriginatingPlatform] == platform));
        await _stripeAdapter.Received(1).CreateCustomerAsync(Arg.Is<CustomerCreateOptions>(options =>
            options.Email == user.Email
            && options.Description == user.Name
            && options.Metadata[StripeConstants.MetadataKeys.Region] == _globalSettings.BaseServiceUri.CloudRegion));
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(savedUser =>
            savedUser.GatewayCustomerId == "cus_123"));
    }

    [Theory]
    [BitAutoData]
    public async Task Run_UserNotPremium_UserHasExistingStripeCustomer_ReturnsCheckoutSessionUrl(User user)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = "cus_existing";
        const string appVersion = "2.0.0";
        const string platform = "Android";

        var existingCustomer = new Customer { Id = "cus_existing" };
        _subscriberService.GetCustomerOrThrow(user).Returns(existingCustomer);

        const string checkoutSessionUrl = "https://checkout.stripe.com/session/456";
        _stripeAdapter.CreateCheckoutSessionAsync(Arg.Any<SessionCreateOptions>()).Returns(new Session { Url = checkoutSessionUrl });

        // Act
        var result = await _command.Run(user, appVersion, platform);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(checkoutSessionUrl, result.AsT0.CheckoutSessionUrl);
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _userService.DidNotReceive().SaveUserAsync(Arg.Any<User>());
        await _stripeAdapter.Received(1).CreateCheckoutSessionAsync(Arg.Is<SessionCreateOptions>(options =>
            options.Customer == existingCustomer.Id
            && options.Mode == StripeConstants.CheckoutSession.Modes.Subscription
            && options.LineItems[0].Price == StripeConstants.Prices.PremiumAnnually
            && options.LineItems[0].Quantity == 1
            && options.AutomaticTax.Enabled == true
            && options.SuccessUrl == _successUrl
            && options.CancelUrl == _cancelUrl
            && options.SubscriptionData.Metadata[StripeConstants.MetadataKeys.UserId] == user.Id.ToString()
            && options.SubscriptionData.Metadata[StripeConstants.MetadataKeys.OriginatingAppVersion] == appVersion
            && options.SubscriptionData.Metadata[StripeConstants.MetadataKeys.OriginatingPlatform] == platform));
    }

    [Theory]
    [BitAutoData]
    public async Task Run_UserIsPremium_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;

        // Act
        var result = await _command.Run(user, "1.0.0", "iOS");

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User is already a premium user.", badRequest.Response);
        await _stripeAdapter.DidNotReceive().CreateCustomerAsync(Arg.Any<CustomerCreateOptions>());
        await _stripeAdapter.DidNotReceive().CreateCheckoutSessionAsync(Arg.Any<SessionCreateOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task Run_GetCustomerOrThrowThrows_ReturnsUnhandled(User user)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = "cus_existing";

        _subscriberService.GetCustomerOrThrow(user).ThrowsAsync(new BillingException());

        // Act
        var result = await _command.Run(user, "1.0.0", "iOS");

        // Assert
        Assert.True(result.IsT3);
        Assert.IsType<BillingException>(result.AsT3.Exception);
        await _stripeAdapter.DidNotReceive().CreateCheckoutSessionAsync(Arg.Any<SessionCreateOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task Run_GetAvailablePremiumPlanThrows_ReturnsUnhandled(User user)
    {
        // Arrange
        user.Premium = false;
        user.GatewayCustomerId = null;

        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(new Customer { Id = "cus_123" });
        _pricingClient.GetAvailablePremiumPlan().ThrowsAsync<NotFoundException>();

        // Act
        var result = await _command.Run(user, "1.0.0", "iOS");

        // Assert
        Assert.True(result.IsT3); // UnhandledException
        Assert.IsType<NotFoundException>(result.AsT3.Exception);
        await _stripeAdapter.DidNotReceive().CreateCheckoutSessionAsync(Arg.Any<SessionCreateOptions>());
    }

}
