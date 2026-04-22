using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Pricing;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentFailedHandler : IPaymentFailedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IPricingClient _pricingClient;
    private readonly ILogger<PaymentFailedHandler> _logger;

    public PaymentFailedHandler(
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        IStripeEventUtilityService stripeEventUtilityService,
        IPricingClient pricingClient,
        ILogger<PaymentFailedHandler> logger)
    {
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _stripeEventUtilityService = stripeEventUtilityService;
        _pricingClient = pricingClient;
        _logger = logger;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.PaymentFailed"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (invoice.Status == StripeConstants.InvoiceStatus.Paid || invoice.AttemptCount <= 1 || !ShouldAttemptToPayInvoice(invoice))
        {
            return;
        }

        if (invoice.Parent?.SubscriptionDetails != null)
        {
            var subscription = await _stripeFacade.GetSubscription(invoice.Parent.SubscriptionDetails.SubscriptionId);
            // attempt count 4 = 11 days after initial failure
            if (invoice.AttemptCount <= 3 || !await IsPremiumSubscriptionAsync(subscription))
            {
                await _stripeEventUtilityService.AttemptToPayInvoiceAsync(invoice);
            }
        }
    }

    // Identifies Premium subscriptions by matching the Password Manager seat Stripe price ID
    // against the set of known Premium plans from the pricing service. Matches on seat only —
    // storage is an add-on, not an identity signal — so this aligns with UpcomingInvoiceHandler's
    // convention. Fails closed (returns true = "assume Premium, stop retrying") on pricing-service
    // errors or empty plan lists so we don't hammer Stripe with pay retries when we can't
    // determine status.
    private async Task<bool> IsPremiumSubscriptionAsync(Subscription subscription)
    {
        List<Bit.Core.Billing.Pricing.Premium.Plan> premiumPlans;
        try
        {
            premiumPlans = await _pricingClient.ListPremiumPlans();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to list Premium plans while evaluating subscription ({SubscriptionId}); halting pay retries",
                subscription.Id);
            return true;
        }

        var premiumSeatPriceIds = premiumPlans
            .Select(p => p.Seat?.StripePriceId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet();

        if (premiumSeatPriceIds.Count == 0)
        {
            _logger.LogError(
                "Pricing service returned no usable Premium seat price IDs while evaluating subscription ({SubscriptionId}); halting pay retries",
                subscription.Id);
            return true;
        }

        return subscription.Items.Any(i => premiumSeatPriceIds.Contains(i.Price.Id));
    }

    private static bool ShouldAttemptToPayInvoice(Invoice invoice) =>
        invoice is
        {
            AmountDue: > 0,
            Status: not StripeConstants.InvoiceStatus.Paid,
            CollectionMethod: "charge_automatically",
            BillingReason: "subscription_cycle" or "automatic_pending_invoice_item_invoice",
            Parent.SubscriptionDetails: not null
        };
}
