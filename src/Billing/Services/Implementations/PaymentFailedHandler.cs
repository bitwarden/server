using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentFailedHandler : IPaymentFailedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;

    public PaymentFailedHandler(
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        IStripeEventUtilityService stripeEventUtilityService
    )
    {
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _stripeEventUtilityService = stripeEventUtilityService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.PaymentFailed"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (invoice.Paid || invoice.AttemptCount <= 1 || !ShouldAttemptToPayInvoice(invoice))
        {
            return;
        }

        var subscription = await _stripeFacade.GetSubscription(invoice.SubscriptionId);
        // attempt count 4 = 11 days after initial failure
        if (
            invoice.AttemptCount <= 3
            || !subscription.Items.Any(i =>
                i.Price.Id
                    is IStripeEventUtilityService.PremiumPlanId
                        or IStripeEventUtilityService.PremiumPlanIdAppStore
            )
        )
        {
            await _stripeEventUtilityService.AttemptToPayInvoiceAsync(invoice);
        }
    }

    private static bool ShouldAttemptToPayInvoice(Invoice invoice) =>
        invoice
            is {
                AmountDue: > 0,
                Paid: false,
                CollectionMethod: "charge_automatically",
                BillingReason: "subscription_cycle" or "automatic_pending_invoice_item_invoice",
                SubscriptionId: not null
            };
}
