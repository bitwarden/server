using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class InvoiceCreatedHandler : IInvoiceCreatedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IProviderEventService _providerEventService;

    public InvoiceCreatedHandler(
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService,
        IProviderEventService providerEventService)
    {
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _providerEventService = providerEventService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.InvoiceCreated"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await _stripeEventService.GetInvoice(parsedEvent, true);
        if (_stripeEventUtilityService.ShouldAttemptToPayInvoice(invoice))
        {
            await _stripeEventUtilityService.AttemptToPayInvoiceAsync(invoice);
        }

        await _providerEventService.TryRecordInvoiceLineItems(parsedEvent);
    }
}
