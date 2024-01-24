using Bit.Billing.Constants;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentFailedHandler : IWebhookEventHandler
{
    private const string _premiumPlanId = "premium-annually";
    private const string _premiumPlanIdAppStore = "premium-annually-app";

    private readonly IStripeEventService _stripeEventService;
    private readonly IWebhookUtility _webhookUtility;

    public PaymentFailedHandler(IStripeEventService stripeEventService,
        IWebhookUtility webhookUtility)
    {
        _stripeEventService = stripeEventService;
        _webhookUtility = webhookUtility;
    }
    public bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.PaymentSucceeded);
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        await HandlePaymentFailedAsync(await _stripeEventService.GetInvoice(parsedEvent, true));
    }

    private async Task HandlePaymentFailedAsync(Invoice invoice)
    {
        if (!invoice.Paid && invoice.AttemptCount > 1 && _webhookUtility.UnpaidAutoChargeInvoiceForSubscriptionCycle(invoice))
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
            // attempt count 4 = 11 days after initial failure
            if (invoice.AttemptCount <= 3 ||
                !subscription.Items.Any(i => i.Price.Id is _premiumPlanId or _premiumPlanIdAppStore))
            {
                await _webhookUtility.AttemptToPayInvoice(invoice);
            }
        }
    }
}
