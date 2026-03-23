using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Stripe.Checkout;
using Xunit;
using Event = Stripe.Event;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Billing.Test.Services;

public class CheckoutSessionCompletedHandlerTests
{
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly string _sessionId = "cs_test_123";
    private static readonly string _subscriptionId = "sub_test_123";
    private static readonly string _premiumSeatPriceId = "price_premium_seat";
    private static readonly Event _mockEvent = new() { Id = "evt_test", Type = "checkout.session.completed" };

    private readonly ILogger<CheckoutSessionCompletedHandler> _logger;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IUserRepository _userRepository;
    private readonly IPricingClient _pricingClient;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;
    private readonly IStripeEventService _stripeEventService;
    private readonly CheckoutSessionCompletedHandler _sut;

    private readonly PremiumPlan _premiumPlan = new()
    {
        Name = "Premium",
        Available = true,
        Seat = new PremiumPurchasable { StripePriceId = _premiumSeatPriceId, Price = 10m, Provided = 1 },
        Storage = new PremiumPurchasable { StripePriceId = "price_storage", Price = 4m, Provided = 1 }
    };

    public CheckoutSessionCompletedHandlerTests()
    {
        _logger = Substitute.For<ILogger<CheckoutSessionCompletedHandler>>();
        _stripeAdapter = Substitute.For<IStripeAdapter>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
        _userRepository = Substitute.For<IUserRepository>();
        _pricingClient = Substitute.For<IPricingClient>();
        _pushNotificationAdapter = Substitute.For<IPushNotificationAdapter>();
        _stripeEventService = Substitute.For<IStripeEventService>();

        _sut = new CheckoutSessionCompletedHandler(
            _stripeEventService,
            _stripeEventUtilityService,
            _stripeAdapter,
            _userRepository,
            _pricingClient,
            _pushNotificationAdapter,
            _logger);
    }

    [Fact]
    public async Task HandleAsync_SessionHasNoSubscriptionId_LogsWarningAndReturns()
    {
        _stripeEventService.GetCheckoutSession(_mockEvent, true)
            .Returns(new Session { Id = _sessionId });

        await _sut.HandleAsync(_mockEvent);

        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(null!);
        await _userRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(null!);
    }

    [Fact]
    public async Task HandleAsync_SubscriptionNotFound_LogsErrorAndReturns()
    {
        _stripeEventService.GetCheckoutSession(_mockEvent, true)
            .Returns(new Session { Id = _sessionId, SubscriptionId = _subscriptionId });
        _stripeAdapter.GetSubscriptionAsync(_subscriptionId).Returns((Subscription)null!);

        await _sut.HandleAsync(_mockEvent);

        await _userRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(null!);
    }

    [Fact]
    public async Task HandleAsync_NoUserIdInMetadata_LogsWarningAndReturns()
    {
        var subscription = new Subscription { Id = _subscriptionId, Metadata = [] };

        _stripeEventService.GetCheckoutSession(_mockEvent, true)
            .Returns(new Session { Id = _sessionId, SubscriptionId = _subscriptionId });
        _stripeAdapter.GetSubscriptionAsync(_subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, null, null));

        await _sut.HandleAsync(_mockEvent);

        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _userRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(null!);
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_LogsErrorAndReturns()
    {
        var subscription = new Subscription { Id = _subscriptionId, Metadata = [] };

        _stripeEventService.GetCheckoutSession(_mockEvent, true)
            .Returns(new Session { Id = _sessionId, SubscriptionId = _subscriptionId });
        _stripeAdapter.GetSubscriptionAsync(_subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns((User)null!);

        await _sut.HandleAsync(_mockEvent);

        await _userRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(null!);
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyPremiumStatusChangedAsync(null!);
    }

    [Fact]
    public async Task HandleAsync_SubscriptionIsNotPremium_LogsWarningAndReturns()
    {
        var nonPremiumItem = new SubscriptionItem { Price = new Price { Id = "price_other_product" } };
        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Metadata = [],
            Items = new StripeList<SubscriptionItem> { Data = [nonPremiumItem] }
        };
        var user = new User { Id = _userId };

        _stripeEventService.GetCheckoutSession(_mockEvent, true)
            .Returns(new Session { Id = _sessionId, SubscriptionId = _subscriptionId });
        _stripeAdapter.GetSubscriptionAsync(_subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.GetAvailablePremiumPlan().Returns(_premiumPlan);

        await _sut.HandleAsync(_mockEvent);

        await _userRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(null!);
        await _pushNotificationAdapter.DidNotReceiveWithAnyArgs().NotifyPremiumStatusChangedAsync(null!);
    }

    [Fact]
    public async Task HandleAsync_ValidSession_SetsPremiumAndNotifiesUser()
    {
        var periodEnd = DateTime.UtcNow.AddYears(1);
        var premiumItem = new SubscriptionItem
        {
            Price = new Price { Id = _premiumSeatPriceId },
            CurrentPeriodEnd = periodEnd
        };
        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Metadata = [],
            Items = new StripeList<SubscriptionItem> { Data = [premiumItem] }
        };
        var user = new User { Id = _userId, Premium = false };

        _stripeEventService.GetCheckoutSession(_mockEvent, true)
            .Returns(new Session { Id = _sessionId, SubscriptionId = _subscriptionId });
        _stripeAdapter.GetSubscriptionAsync(_subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.GetAvailablePremiumPlan().Returns(_premiumPlan);

        await _sut.HandleAsync(_mockEvent);

        Assert.True(user.Premium);
        Assert.Equal(_subscriptionId, user.GatewaySubscriptionId);
        Assert.Equal(GatewayType.Stripe, user.Gateway);
        Assert.Equal(periodEnd, user.PremiumExpirationDate);
        Assert.Equal((short)_premiumPlan.Storage.Provided, user.MaxStorageGb);
        Assert.NotNull(user.LicenseKey);
        await _userRepository.Received(1).ReplaceAsync(user);
        await _pushNotificationAdapter.Received(1).NotifyPremiumStatusChangedAsync(user);
    }

    [Fact]
    public async Task HandleAsync_UserAlreadyHasLicenseKey_PreservesExistingLicenseKey()
    {
        var existingLicenseKey = "existing-license-key-12";
        var periodEnd = DateTime.UtcNow.AddYears(1);
        var premiumItem = new SubscriptionItem
        {
            Price = new Price { Id = _premiumSeatPriceId },
            CurrentPeriodEnd = periodEnd
        };
        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Metadata = [],
            Items = new StripeList<SubscriptionItem> { Data = [premiumItem] }
        };
        var user = new User { Id = _userId, LicenseKey = existingLicenseKey };

        _stripeEventService.GetCheckoutSession(_mockEvent, true)
            .Returns(new Session { Id = _sessionId, SubscriptionId = _subscriptionId });
        _stripeAdapter.GetSubscriptionAsync(_subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, _userId, null));
        _userRepository.GetByIdAsync(_userId).Returns(user);
        _pricingClient.GetAvailablePremiumPlan().Returns(_premiumPlan);

        await _sut.HandleAsync(_mockEvent);

        Assert.Equal(existingLicenseKey, user.LicenseKey);
        await _userRepository.Received(1).ReplaceAsync(user);
    }
}
