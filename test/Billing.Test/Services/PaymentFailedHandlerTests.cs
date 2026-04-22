using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;
using Event = Stripe.Event;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using Purchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Billing.Test.Services;

public class PaymentFailedHandlerTests
{
    private readonly IStripeEventService _stripeEventService = Substitute.For<IStripeEventService>();
    private readonly IStripeFacade _stripeFacade = Substitute.For<IStripeFacade>();
    private readonly IStripeEventUtilityService _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly PaymentFailedHandler _sut;

    public PaymentFailedHandlerTests()
    {
        _sut = new PaymentFailedHandler(
            _stripeEventService,
            _stripeFacade,
            _stripeEventUtilityService,
            _pricingClient,
            Substitute.For<ILogger<PaymentFailedHandler>>());
    }

    [Fact]
    public async Task HandleAsync_PremiumSubscription_BeyondAttemptLimit_DoesNotAttemptPay()
    {
        // Verifies the hardcoded-price-ID bug is fixed: a subscription on the current
        // `premium-annually-2026` price must be recognized as Premium so that pay-retries
        // correctly stop after attempt 3 (per the original policy for Premium subs).
        var subscriptionId = "sub_123";
        const string currentPremiumPriceId = "premium-annually-2026";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = currentPremiumPriceId } }]
            }
        };

        var invoice = new Invoice
        {
            Status = InvoiceStatus.Open,
            AmountDue = 1980,
            AttemptCount = 4,
            CollectionMethod = "charge_automatically",
            BillingReason = "subscription_cycle",
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId }
            }
        };

        _stripeEventService.GetInvoice(Arg.Any<Event>(), Arg.Any<bool>()).Returns(invoice);
        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns([
            new PremiumPlan
            {
                Seat = new Purchasable { StripePriceId = currentPremiumPriceId },
                Storage = new Purchasable { StripePriceId = "personal-storage-gb-annually" }
            }
        ]);

        await _sut.HandleAsync(new Event());

        await _stripeEventUtilityService.DidNotReceive().AttemptToPayInvoiceAsync(Arg.Any<Invoice>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_PricingServiceThrows_BeyondAttemptLimit_HaltsPayRetries()
    {
        // Fail-closed: if we can't determine whether the subscription is Premium,
        // stop retrying rather than hammering Stripe with repeated pay attempts.
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "some-org-price" } }]
            }
        };

        var invoice = new Invoice
        {
            Status = InvoiceStatus.Open,
            AmountDue = 1980,
            AttemptCount = 4,
            CollectionMethod = "charge_automatically",
            BillingReason = "subscription_cycle",
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId }
            }
        };

        _stripeEventService.GetInvoice(Arg.Any<Event>(), Arg.Any<bool>()).Returns(invoice);
        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns<List<PremiumPlan>>(_ => throw new HttpRequestException("pricing unreachable"));

        await _sut.HandleAsync(new Event());

        await _stripeEventUtilityService.DidNotReceive().AttemptToPayInvoiceAsync(Arg.Any<Invoice>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task HandleAsync_NonPremiumSubscription_BeyondAttemptLimit_StillAttemptsPay()
    {
        var subscriptionId = "sub_123";

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "some-org-price" } }]
            }
        };

        var invoice = new Invoice
        {
            Status = InvoiceStatus.Open,
            AmountDue = 1980,
            AttemptCount = 4,
            CollectionMethod = "charge_automatically",
            BillingReason = "subscription_cycle",
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId }
            }
        };

        _stripeEventService.GetInvoice(Arg.Any<Event>(), Arg.Any<bool>()).Returns(invoice);
        _stripeFacade.GetSubscription(subscriptionId).Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns([
            new PremiumPlan
            {
                Seat = new Purchasable { StripePriceId = "premium-annually-2026" },
                Storage = new Purchasable { StripePriceId = "personal-storage-gb-annually" }
            }
        ]);

        await _sut.HandleAsync(new Event());

        await _stripeEventUtilityService.Received(1).AttemptToPayInvoiceAsync(invoice, Arg.Any<bool>());
    }
}
