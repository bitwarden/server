using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Caches;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class SetupIntentSucceededHandler(
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    IPushNotificationAdapter pushNotificationAdapter,
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    IStripeEventService stripeEventService) : ISetupIntentSucceededHandler
{
    public async Task HandleAsync(Event parsedEvent)
    {
        var setupIntent = await stripeEventService.GetSetupIntent(
            parsedEvent,
            true,
            ["payment_method"]);

        if (setupIntent is not
            {
                PaymentMethod.UsBankAccount: not null
            })
        {
            return;
        }

        var subscriberId = await setupIntentCache.GetSubscriberIdForSetupIntent(setupIntent.Id);
        if (subscriberId == null)
        {
            return;
        }

        var organization = await organizationRepository.GetByIdAsync(subscriberId.Value);
        var provider = await providerRepository.GetByIdAsync(subscriberId.Value);

        OneOf<Organization, Provider> entity = organization != null ? organization : provider!;
        await SetPaymentMethodAsync(entity, setupIntent.PaymentMethod);
    }

    private async Task SetPaymentMethodAsync(
        OneOf<Organization, Provider> subscriber,
        PaymentMethod paymentMethod)
    {
        var customerId = subscriber.Match(
            organization => organization.GatewayCustomerId,
            provider => provider.GatewayCustomerId);

        if (string.IsNullOrEmpty(customerId))
        {
            return;
        }

        await stripeAdapter.PaymentMethodAttachAsync(paymentMethod.Id,
            new PaymentMethodAttachOptions { Customer = customerId });

        await stripeAdapter.CustomerUpdateAsync(customerId, new CustomerUpdateOptions
        {
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                DefaultPaymentMethod = paymentMethod.Id
            }
        });

        await subscriber.Match(
            async organization => await pushNotificationAdapter.NotifyBankAccountVerifiedAsync(organization),
            async provider => await pushNotificationAdapter.NotifyBankAccountVerifiedAsync(provider));
    }
}
