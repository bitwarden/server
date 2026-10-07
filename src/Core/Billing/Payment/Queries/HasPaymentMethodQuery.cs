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
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : IHasPaymentMethodQuery
{
    public async Task<bool> Run(ISubscriber subscriber)
    {
        var customer = await subscriberService.GetCustomer(subscriber);

        if (customer == null)
        {
            return false;
        }

        var hasUnverifiedBankAccount = await HasUnverifiedBankAccountAsync(customer.Id);

        return
            !string.IsNullOrEmpty(customer.InvoiceSettings.DefaultPaymentMethodId) ||
            !string.IsNullOrEmpty(customer.DefaultSourceId) ||
            hasUnverifiedBankAccount ||
            customer.Metadata.ContainsKey(MetadataKeys.BraintreeCustomerId);
    }

    private async Task<bool> HasUnverifiedBankAccountAsync(string customerId)
    {
        var setupIntents = await stripeAdapter.ListSetupIntentsAsync(new SetupIntentListOptions
        {
            Customer = customerId,
            Expand = ["data.payment_method"]
        });

        return setupIntents?.Any(si => si.IsUnverifiedBankAccount()) ?? false;
    }
}
