using Bit.Billing.Constants;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Caches;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Serilog.Data;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class StripeEventService(
    GlobalSettings globalSettings,
    ILogger<StripeEventService> logger,
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    ISetupIntentCache setupIntentCache,
    IStripeFacade stripeFacade)
    : IStripeEventService
{
    public async Task<Charge> GetCharge(Event stripeEvent, bool fresh = false, List<string>? expand = null)
    {
        var charge = Extract<Charge>(stripeEvent);

        if (!fresh)
        {
            return charge;
        }

        return await stripeFacade.GetCharge(charge.Id, new ChargeGetOptions { Expand = expand });
    }

    public async Task<Customer> GetCustomer(Event stripeEvent, bool fresh = false, List<string>? expand = null)
    {
        var customer = Extract<Customer>(stripeEvent);

        if (!fresh)
        {
            return customer;
        }

        return await stripeFacade.GetCustomer(customer.Id, new CustomerGetOptions { Expand = expand });
    }

    public async Task<Invoice> GetInvoice(Event stripeEvent, bool fresh = false, List<string>? expand = null)
    {
        var invoice = Extract<Invoice>(stripeEvent);

        if (!fresh)
        {
            return invoice;
        }

        return await stripeFacade.GetInvoice(invoice.Id, new InvoiceGetOptions { Expand = expand });
    }

    public async Task<PaymentMethod> GetPaymentMethod(Event stripeEvent, bool fresh = false,
        List<string>? expand = null)
    {
        var paymentMethod = Extract<PaymentMethod>(stripeEvent);

        if (!fresh)
        {
            return paymentMethod;
        }

        return await stripeFacade.GetPaymentMethod(paymentMethod.Id, new PaymentMethodGetOptions { Expand = expand });
    }

    public async Task<SetupIntent> GetSetupIntent(Event stripeEvent, bool fresh = false, List<string>? expand = null)
    {
        var setupIntent = Extract<SetupIntent>(stripeEvent);

        if (!fresh)
        {
            return setupIntent;
        }

        return await stripeFacade.GetSetupIntent(setupIntent.Id, new SetupIntentGetOptions { Expand = expand });
    }

    public async Task<Subscription> GetSubscription(Event stripeEvent, bool fresh = false, List<string>? expand = null)
    {
        var subscription = Extract<Subscription>(stripeEvent);

        if (!fresh)
        {
            return subscription;
        }

        return await stripeFacade.GetSubscription(subscription.Id, new SubscriptionGetOptions { Expand = expand });
    }

    public async Task<bool> ValidateCloudRegion(Event stripeEvent)
    {
        logger.LogInformation("Validating cloud region for Stripe event ({ID}) with type '{Type}'", stripeEvent.Id,
            stripeEvent.Type);

        var serverRegion = globalSettings.BaseServiceUri.CloudRegion;

        var customerExpansion = new List<string> { "customer" };

        var customerMetadata = stripeEvent.Type switch
        {
            HandledStripeWebhook.SubscriptionDeleted or HandledStripeWebhook.SubscriptionUpdated =>
                (await GetSubscription(stripeEvent, true, customerExpansion)).Customer?.Metadata,

            HandledStripeWebhook.ChargeSucceeded or HandledStripeWebhook.ChargeRefunded =>
                (await GetCharge(stripeEvent, true, customerExpansion)).Customer?.Metadata,

            HandledStripeWebhook.UpcomingInvoice =>
                await GetCustomerMetadataFromUpcomingInvoiceEvent(stripeEvent),

            HandledStripeWebhook.PaymentSucceeded or HandledStripeWebhook.PaymentFailed
                or HandledStripeWebhook.InvoiceCreated or HandledStripeWebhook.InvoiceFinalized =>
                (await GetInvoice(stripeEvent, true, customerExpansion)).Customer?.Metadata,

            HandledStripeWebhook.PaymentMethodAttached =>
                (await GetPaymentMethod(stripeEvent, true, customerExpansion)).Customer?.Metadata,

            HandledStripeWebhook.CustomerUpdated =>
                (await GetCustomer(stripeEvent, true)).Metadata,

            HandledStripeWebhook.SetupIntentSucceeded =>
                await GetCustomerMetadataFromSetupIntentSucceededEvent(stripeEvent),

            _ => null
        };

        if (customerMetadata == null)
        {
            logger.LogWarning("Customer metadata was null for Stripe event ({ID})", stripeEvent.Id);
            return false;
        }

        var customerRegion = GetCustomerRegion(customerMetadata);

        return customerRegion == serverRegion;

        /* In Stripe, when we receive an invoice.upcoming event, the event does not include an Invoice ID because
           the invoice hasn't been created yet. Therefore, rather than retrieving the fresh Invoice with a 'customer'
           expansion, we need to use the Customer ID on the event to retrieve the metadata. */
        async Task<Dictionary<string, string>?> GetCustomerMetadataFromUpcomingInvoiceEvent(Event localStripeEvent)
        {
            var invoice = await GetInvoice(localStripeEvent);

            var customer = !string.IsNullOrEmpty(invoice.CustomerId)
                ? await stripeFacade.GetCustomer(invoice.CustomerId)
                : null;

            return customer?.Metadata;
        }

        async Task<Dictionary<string, string>?> GetCustomerMetadataFromSetupIntentSucceededEvent(Event localStripeEvent)
        {
            logger.LogInformation("Getting Customer metadata for setup_intent.succeeded event ({ID})", localStripeEvent.Id);

            var setupIntent = await GetSetupIntent(localStripeEvent);

            var subscriberId = await setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id);
            if (subscriberId == null)
            {
                logger.LogWarning("No subscriber ID was found for setup_intent.succeeded event ({ID})",
                    localStripeEvent.Id);
                return null;
            }

            var organization = await organizationRepository.GetByIdAsync(subscriberId.Value);
            if (organization is { GatewayCustomerId: not null })
            {
                var organizationCustomer = await stripeFacade.GetCustomer(organization.GatewayCustomerId);
                return organizationCustomer.Metadata;
            }

            var provider = await providerRepository.GetByIdAsync(subscriberId.Value);
            if (provider is not { GatewayCustomerId: not null })
            {
                return null;
            }

            var providerCustomer = await stripeFacade.GetCustomer(provider.GatewayCustomerId);
            return providerCustomer.Metadata;
        }
    }

    private static T Extract<T>(Event stripeEvent)
        => stripeEvent.Data.Object is not T type
            ? throw new Exception(
                $"Stripe event with ID '{stripeEvent.Id}' does not have object matching type '{typeof(T).Name}'")
            : type;

    private static string GetCustomerRegion(IDictionary<string, string> customerMetadata)
    {
        const string defaultRegion = Core.Constants.CountryAbbreviations.UnitedStates;

        if (customerMetadata.TryGetValue("region", out var value))
        {
            return value;
        }

        var incorrectlyCasedRegionKey = customerMetadata.Keys
            .FirstOrDefault(key => key.Equals("region", StringComparison.OrdinalIgnoreCase));

        if (incorrectlyCasedRegionKey is null)
        {
            return defaultRegion;
        }

        _ = customerMetadata.TryGetValue(incorrectlyCasedRegionKey, out var regionValue);

        return !string.IsNullOrWhiteSpace(regionValue)
            ? regionValue
            : defaultRegion;
    }
}
