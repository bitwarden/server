using Bit.Billing.Constants;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentFailedHandler : StripeWebhookHandler
{
    private readonly IStripeEventService _stripeEventService;

    public PaymentFailedHandler(IStripeEventService stripeEventService)
    {
        _stripeEventService = stripeEventService;
    }
    protected override bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.PaymentSucceeded);
    }

    protected override async Task<IActionResult> ProcessEvent(Event parsedEvent)
    {
        await HandlePaymentFailedAsync(await _stripeEventService.GetInvoice(parsedEvent, true));
        return new OkResult();
    }

    private async Task HandlePaymentFailedAsync(Invoice invoice)
    {
        if (!invoice.Paid && invoice.AttemptCount > 1 && UnpaidAutoChargeInvoiceForSubscriptionCycle(invoice))
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(invoice.SubscriptionId);
            // attempt count 4 = 11 days after initial failure
            if (invoice.AttemptCount <= 3 ||
                !subscription.Items.Any(i => i.Price.Id is PremiumPlanId or PremiumPlanIdAppStore))
            {
                await AttemptToPayInvoiceAsync(invoice);
            }
        }
    }


}
