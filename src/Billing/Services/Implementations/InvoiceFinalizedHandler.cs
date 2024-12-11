using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class InvoiceFinalizedHandler : IInvoiceFinalizedHandler
{
    private readonly IProviderEventService _providerEventService;

    public InvoiceFinalizedHandler(IProviderEventService providerEventService)
    {
        _providerEventService = providerEventService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.InvoiceFinalized"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        await _providerEventService.TryRecordInvoiceLineItems(parsedEvent);
    }
}
