using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;
using Event = Stripe.Event;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using Purchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Billing.Test.Services;

public class PaymentSucceededHandlerTests
{
    private readonly IStripeEventService _stripeEventService = Substitute.For<IStripeEventService>();
    private readonly IStripeFacade _stripeFacade = Substitute.For<IStripeFacade>();
    private readonly IProviderRepository _providerRepository = Substitute.For<IProviderRepository>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IStripeEventUtilityService _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IOrganizationEnableCommand _organizationEnableCommand = Substitute.For<IOrganizationEnableCommand>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IPushNotificationAdapter _pushNotificationAdapter = Substitute.For<IPushNotificationAdapter>();
    private readonly PaymentSucceededHandler _sut;

    public PaymentSucceededHandlerTests()
    {
        _sut = new PaymentSucceededHandler(
            Substitute.For<ILogger<PaymentSucceededHandler>>(),
            _stripeEventService,
            _stripeFacade,
            _providerRepository,
            _organizationRepository,
            _stripeEventUtilityService,
            _userService,
            _userRepository,
            _organizationEnableCommand,
            _pricingClient,
            _pushNotificationAdapter);
    }

    [Fact]
    public async Task HandleAsync_UserSubscription_WithCurrentPremiumPriceId_EnablesPremium()
    {
        // Verifies the hardcoded-price-ID bug is fixed: a subscription on the current
        // `premium-annually-2026` price (not the legacy `premium-annually` constant) must
        // still be recognized as Premium by the pricing-service lookup.
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";
        const string currentPremiumPriceId = "premium-annually-2026";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = currentPremiumPriceId } }]
            },
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        };

        var invoice = new Invoice
        {
            Status = InvoiceStatus.Paid,
            BillingReason = "subscription_create",
            Created = DateTime.UtcNow.AddMinutes(-5),
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId }
            }
        };

        _stripeEventService.GetInvoice(Arg.Any<Event>(), Arg.Any<bool>()).Returns(invoice);
        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, userId, null));
        _pricingClient.ListPremiumPlans().Returns([
            new PremiumPlan
            {
                Seat = new Purchasable { StripePriceId = currentPremiumPriceId },
                Storage = new Purchasable { StripePriceId = "personal-storage-gb-annually" }
            }
        ]);
        _userRepository.GetByIdAsync(userId).Returns(new User { Id = userId });

        await _sut.HandleAsync(new Event());

        await _userService.Received(1).EnablePremiumAsync(userId, Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task HandleAsync_UserSubscription_PricingServiceThrows_DoesNotEnablePremium()
    {
        // Fail-safe: if the pricing service can't tell us which plans are Premium,
        // we don't risk incorrectly enabling Premium. The 500-retry loop that would
        // otherwise ensue is the exact pattern PM-33289 fixes against.
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "premium-annually-2026" } }]
            },
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        };

        var invoice = new Invoice
        {
            Status = InvoiceStatus.Paid,
            BillingReason = "subscription_create",
            Created = DateTime.UtcNow.AddMinutes(-5),
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId }
            }
        };

        _stripeEventService.GetInvoice(Arg.Any<Event>(), Arg.Any<bool>()).Returns(invoice);
        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, userId, null));
        _pricingClient.ListPremiumPlans().Returns<List<PremiumPlan>>(_ => throw new HttpRequestException("pricing unreachable"));

        await _sut.HandleAsync(new Event());

        await _userService.DidNotReceive().EnablePremiumAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task HandleAsync_UserSubscription_PricingServiceReturnsEmptyList_DoesNotEnablePremium()
    {
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "premium-annually-2026" } }]
            },
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        };

        var invoice = new Invoice
        {
            Status = InvoiceStatus.Paid,
            BillingReason = "subscription_create",
            Created = DateTime.UtcNow.AddMinutes(-5),
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId }
            }
        };

        _stripeEventService.GetInvoice(Arg.Any<Event>(), Arg.Any<bool>()).Returns(invoice);
        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, userId, null));
        _pricingClient.ListPremiumPlans().Returns([]);

        await _sut.HandleAsync(new Event());

        await _userService.DidNotReceive().EnablePremiumAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task HandleAsync_UserSubscription_WithoutPremiumPriceId_DoesNotEnablePremium()
    {
        var userId = Guid.NewGuid();
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "some-other-price" } }]
            },
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        };

        var invoice = new Invoice
        {
            Status = InvoiceStatus.Paid,
            BillingReason = "subscription_create",
            Created = DateTime.UtcNow.AddMinutes(-5),
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId }
            }
        };

        _stripeEventService.GetInvoice(Arg.Any<Event>(), Arg.Any<bool>()).Returns(invoice);
        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(null, userId, null));
        _pricingClient.ListPremiumPlans().Returns([
            new PremiumPlan
            {
                Seat = new Purchasable { StripePriceId = "premium-annually-2026" },
                Storage = new Purchasable { StripePriceId = "personal-storage-gb-annually" }
            }
        ]);

        await _sut.HandleAsync(new Event());

        await _userService.DidNotReceive().EnablePremiumAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
    }
}
