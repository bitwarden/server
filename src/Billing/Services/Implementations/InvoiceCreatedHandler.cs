using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class InvoiceCreatedHandler : IInvoiceCreatedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;

    public InvoiceCreatedHandler(
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService)
    {
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.InvoiceCreated"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (invoice.Paid || !_stripeEventUtilityService.ShouldAttemptToPayInvoice(invoice))
        {
            return;
        }

        await _stripeEventUtilityService.AttemptToPayInvoiceAsync(invoice);
    }
}
