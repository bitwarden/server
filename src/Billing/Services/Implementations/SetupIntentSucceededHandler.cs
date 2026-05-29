using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Repositories;
using OneOf;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class SetupIntentSucceededHandler(
    ILogger<SetupIntentSucceededHandler> logger,
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    IPushNotificationAdapter pushNotificationAdapter,
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
                CustomerId: not null,
                PaymentMethod.UsBankAccount: not null
            })
        {
            logger.LogWarning("SetupIntent {SetupIntentId} has no customer ID or is not a US bank account", setupIntent.Id);
            return;
        }

        var organization = await organizationRepository.GetByGatewayCustomerIdAsync(setupIntent.CustomerId);
        if (organization != null)
        {
            await SetPaymentMethodAsync(organization, setupIntent.PaymentMethod);
            return;
        }

        var provider = await providerRepository.GetByGatewayCustomerIdAsync(setupIntent.CustomerId);
        if (provider != null)
        {
            await SetPaymentMethodAsync(provider, setupIntent.PaymentMethod);
            return;
        }

        logger.LogError("No organization or provider found for customer {CustomerId}", setupIntent.CustomerId);
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

        await stripeAdapter.AttachPaymentMethodAsync(paymentMethod.Id,
            new PaymentMethodAttachOptions { Customer = customerId });

        await stripeAdapter.UpdateCustomerAsync(customerId, new CustomerUpdateOptions
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
