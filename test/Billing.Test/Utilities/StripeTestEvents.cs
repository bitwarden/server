using Stripe;

namespace Bit.Billing.Test.Utilities;

public enum StripeEventType
{
    ChargeSucceeded,
    CustomerSubscriptionUpdated,
    CustomerUpdated,
    InvoiceCreated,
    InvoiceFinalized,
    InvoiceUpcoming,
    PaymentMethodAttached,
}

public static class StripeTestEvents
{
    public static async Task<Event> GetAsync(StripeEventType eventType)
    {
        var fileName = eventType switch
        {
            StripeEventType.ChargeSucceeded => "charge.succeeded.json",
            StripeEventType.CustomerSubscriptionUpdated => "customer.subscription.updated.json",
            StripeEventType.CustomerUpdated => "customer.updated.json",
            StripeEventType.InvoiceCreated => "invoice.created.json",
            StripeEventType.InvoiceFinalized => "invoice.finalized.json",
            StripeEventType.InvoiceUpcoming => "invoice.upcoming.json",
            StripeEventType.PaymentMethodAttached => "payment_method.attached.json",
        };

        var resource = await EmbeddedResourceReader.ReadAsync("Events", fileName);

        return EventUtility.ParseEvent(resource);
    }
}
