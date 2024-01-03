using Bit.Billing.Constants;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class InvoiceCreatedHandler : StripeWebhookHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IWebhookUtility _webhookUtility;

    public InvoiceCreatedHandler(IStripeEventService stripeEventService,
        IWebhookUtility webhookUtility)
    {
        _stripeEventService = stripeEventService;
        _webhookUtility = webhookUtility;
    }
    protected override bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.InvoiceCreated);
    }

    protected override async Task<IActionResult> ProcessEvent(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (!invoice.Paid && _webhookUtility.UnpaidAutoChargeInvoiceForSubscriptionCycle(invoice))
        {
            await _webhookUtility.AttemptToPayInvoice(invoice);
        }

        return new OkResult();
    }
}
