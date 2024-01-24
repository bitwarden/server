using Bit.Billing.Constants;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class InvoiceCreatedHandler : IWebhookEventHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IWebhookUtility _webhookUtility;

    public InvoiceCreatedHandler(IStripeEventService stripeEventService,
        IWebhookUtility webhookUtility)
    {
        _stripeEventService = stripeEventService;
        _webhookUtility = webhookUtility;
    }
    public bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.InvoiceCreated);
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (!invoice.Paid && _webhookUtility.UnpaidAutoChargeInvoiceForSubscriptionCycle(invoice))
        {
            await _webhookUtility.AttemptToPayInvoice(invoice);
        }
    }
}
