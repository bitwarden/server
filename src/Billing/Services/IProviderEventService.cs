using Stripe;

namespace Bit.Billing.Services;

public interface IProviderEventService
{
    Task TryRecordInvoiceLineItems(Event parsedEvent);
}
