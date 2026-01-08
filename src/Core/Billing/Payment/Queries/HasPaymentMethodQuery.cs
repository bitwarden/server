using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Billing.Payment.Queries;

using static StripeConstants;

public interface IHasPaymentMethodQuery
{
    Task<bool> Run(ISubscriber subscriber);
}

public class HasPaymentMethodQuery(
    ISetupIntentCache setupIntentCache,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IHasPaymentMethodQuery
{
    public async Task<bool> Run(ISubscriber subscriber)
    {
        var hasUnverifiedBankAccount = await HasUnverifiedBankAccountAsync(subscriber);

        var customer = await subscriberService.GetCustomer(subscriber);

        if (customer == null)
        {
            return hasUnverifiedBankAccount;
        }

        return
            !string.IsNullOrEmpty(customer.InvoiceSettings.DefaultPaymentMethodId) ||
            !string.IsNullOrEmpty(customer.DefaultSourceId) ||
            hasUnverifiedBankAccount ||
            customer.Metadata.ContainsKey(MetadataKeys.BraintreeCustomerId);
    }

    private async Task<bool> HasUnverifiedBankAccountAsync(
        ISubscriber subscriber)
    {
        var setupIntentId = await setupIntentCache.GetSetupIntentIdForSubscriber(subscriber.Id);

        if (string.IsNullOrEmpty(setupIntentId))
        {
            return false;
        }

        var setupIntent = await stripeAdapter.GetSetupIntentAsync(setupIntentId, new SetupIntentGetOptions
        {
            Expand = ["payment_method"]
        });

        return setupIntent.IsUnverifiedBankAccount();
    }
}
