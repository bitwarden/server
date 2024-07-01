using Bit.Billing.Constants;
using Bit.Core.Settings;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class StripeEventService : IStripeEventService
{
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<StripeEventService> _logger;
    private readonly IStripeFacade _stripeFacade;

    public StripeEventService(
        GlobalSettings globalSettings,
        ILogger<StripeEventService> logger,
        IStripeFacade stripeFacade)
    {
        _globalSettings = globalSettings;
        _logger = logger;
        _stripeFacade = stripeFacade;
    }

    public async Task<Charge> GetCharge(Event stripeEvent, bool fresh = false, List<string> expand = null)
    {
        var eventCharge = Extract<Charge>(stripeEvent);

        if (!fresh)
        {
            return eventCharge;
        }

        if (string.IsNullOrEmpty(eventCharge.Id))
        {
            _logger.LogWarning("Cannot retrieve up-to-date Charge for Event with ID '{eventId}' because no Charge ID was included in the Event.", stripeEvent.Id);
            return eventCharge;
        }

        var charge = await _stripeFacade.GetCharge(eventCharge.Id, new ChargeGetOptions { Expand = expand });

        if (charge == null)
        {
            throw new Exception(
                $"Received null Charge from Stripe for ID '{eventCharge.Id}' while processing Event with ID '{stripeEvent.Id}'");
        }

        return charge;
    }

    public async Task<Customer> GetCustomer(Event stripeEvent, bool fresh = false, List<string> expand = null)
    {
        var eventCustomer = Extract<Customer>(stripeEvent);

        if (!fresh)
        {
            return eventCustomer;
        }

        if (string.IsNullOrEmpty(eventCustomer.Id))
        {
            _logger.LogWarning("Cannot retrieve up-to-date Customer for Event with ID '{eventId}' because no Customer ID was included in the Event.", stripeEvent.Id);
            return eventCustomer;
        }

        var customer = await _stripeFacade.GetCustomer(eventCustomer.Id, new CustomerGetOptions { Expand = expand });

        if (customer == null)
        {
            throw new Exception(
                $"Received null Customer from Stripe for ID '{eventCustomer.Id}' while processing Event with ID '{stripeEvent.Id}'");
        }

        return customer;
    }

    public async Task<Invoice> GetInvoice(Event stripeEvent, bool fresh = false, List<string> expand = null)
    {
        var eventInvoice = Extract<Invoice>(stripeEvent);

        if (!fresh)
        {
            return eventInvoice;
        }

        if (string.IsNullOrEmpty(eventInvoice.Id))
        {
            _logger.LogWarning("Cannot retrieve up-to-date Invoice for Event with ID '{eventId}' because no Invoice ID was included in the Event.", stripeEvent.Id);
            return eventInvoice;
        }

        var invoice = await _stripeFacade.GetInvoice(eventInvoice.Id, new InvoiceGetOptions { Expand = expand });

        if (invoice == null)
        {
            throw new Exception(
                $"Received null Invoice from Stripe for ID '{eventInvoice.Id}' while processing Event with ID '{stripeEvent.Id}'");
        }

        return invoice;
    }

    public async Task<PaymentMethod> GetPaymentMethod(Event stripeEvent, bool fresh = false, List<string> expand = null)
    {
        var eventPaymentMethod = Extract<PaymentMethod>(stripeEvent);

        if (!fresh)
        {
            return eventPaymentMethod;
        }

        if (string.IsNullOrEmpty(eventPaymentMethod.Id))
        {
            _logger.LogWarning("Cannot retrieve up-to-date Payment Method for Event with ID '{eventId}' because no Payment Method ID was included in the Event.", stripeEvent.Id);
            return eventPaymentMethod;
        }

        var paymentMethod = await _stripeFacade.GetPaymentMethod(eventPaymentMethod.Id, new PaymentMethodGetOptions { Expand = expand });

        if (paymentMethod == null)
        {
            throw new Exception(
                $"Received null Payment Method from Stripe for ID '{eventPaymentMethod.Id}' while processing Event with ID '{stripeEvent.Id}'");
        }

        return paymentMethod;
    }

    public async Task<Subscription> GetSubscription(Event stripeEvent, bool fresh = false, List<string> expand = null)
    {
        var eventSubscription = Extract<Subscription>(stripeEvent);

        if (!fresh)
        {
            return eventSubscription;
        }

        if (string.IsNullOrEmpty(eventSubscription.Id))
        {
            _logger.LogWarning("Cannot retrieve up-to-date Subscription for Event with ID '{eventId}' because no Subscription ID was included in the Event.", stripeEvent.Id);
            return eventSubscription;
        }

        var subscription = await _stripeFacade.GetSubscription(eventSubscription.Id, new SubscriptionGetOptions { Expand = expand });

        if (subscription == null)
        {
            throw new Exception(
                $"Received null Subscription from Stripe for ID '{eventSubscription.Id}' while processing Event with ID '{stripeEvent.Id}'");
        }

        return subscription;
    }

    public async Task<bool> ValidateCloudRegion(Event stripeEvent)
    {
        var serverRegion = _globalSettings.BaseServiceUri.CloudRegion;

        var customerExpansion = new List<string> { "customer" };

        var customerMetadata = stripeEvent.Type switch
        {
            HandledStripeWebhook.SubscriptionDeleted or HandledStripeWebhook.SubscriptionUpdated =>
                (await GetSubscription(stripeEvent, true, customerExpansion))?.Customer?.Metadata,

            HandledStripeWebhook.ChargeSucceeded or HandledStripeWebhook.ChargeRefunded =>
                (await GetCharge(stripeEvent, true, customerExpansion))?.Customer?.Metadata,

            HandledStripeWebhook.UpcomingInvoice =>
                await GetCustomerMetadataFromUpcomingInvoiceEvent(stripeEvent),

            HandledStripeWebhook.PaymentSucceeded or HandledStripeWebhook.PaymentFailed or HandledStripeWebhook.InvoiceCreated or HandledStripeWebhook.InvoiceFinalized =>
                (await GetInvoice(stripeEvent, true, customerExpansion))?.Customer?.Metadata,

            HandledStripeWebhook.PaymentMethodAttached =>
                (await GetPaymentMethod(stripeEvent, true, customerExpansion))?.Customer?.Metadata,

            HandledStripeWebhook.CustomerUpdated =>
                (await GetCustomer(stripeEvent, true))?.Metadata,

            _ => null
        };

        if (customerMetadata == null)
        {
            return false;
        }

        var customerRegion = GetCustomerRegion(customerMetadata);

        return customerRegion == serverRegion;

        /* In Stripe, when we receive an invoice.upcoming event, the event does not include an Invoice ID because
           the invoice hasn't been created yet. Therefore, rather than retrieving the fresh Invoice with a 'customer'
           expansion, we need to use the Customer ID on the event to retrieve the metadata. */
        async Task<Dictionary<string, string>> GetCustomerMetadataFromUpcomingInvoiceEvent(Event localStripeEvent)
        {
            var invoice = await GetInvoice(localStripeEvent);

            var customer = !string.IsNullOrEmpty(invoice.CustomerId)
                ? await _stripeFacade.GetCustomer(invoice.CustomerId)
                : null;

            return customer?.Metadata;
        }
    }

    private static T Extract<T>(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not T type)
        {
            throw new Exception($"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{typeof(T).Name}'");
        }

        return type;
    }

    private static string GetCustomerRegion(IDictionary<string, string> customerMetadata)
    {
        const string defaultRegion = "US";

        if (customerMetadata is null)
        {
            return null;
        }

        if (customerMetadata.TryGetValue("region", out var value))
        {
            return value;
        }

        var miscasedRegionKey = customerMetadata.Keys
            .FirstOrDefault(key => key.Equals("region", StringComparison.OrdinalIgnoreCase));

        if (miscasedRegionKey is null)
        {
            return defaultRegion;
        }

        _ = customerMetadata.TryGetValue(miscasedRegionKey, out var regionValue);

        return !string.IsNullOrWhiteSpace(regionValue)
            ? regionValue
            : defaultRegion;
    }
}
