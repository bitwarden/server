#nullable enable
using Bit.Core.Billing.Extensions;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations.AutomaticTax;

public class IndividualAutomaticTaxStrategy : IIndividualAutomaticTaxStrategy
{
    public void SetCreateOptions(SubscriptionCreateOptions options, Customer customer)
    {
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = ShouldBeEnabled(customer)
        };
    }

    public void SetUpdateOptions(SubscriptionUpdateOptions options, Subscription subscription)
    {
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = ShouldBeEnabled(subscription.Customer)
        };
        options.DefaultTaxRates = [];
    }

    public SubscriptionUpdateOptions? GetUpdateOptions(Subscription subscription)
    {
        if (subscription.AutomaticTax.Enabled == ShouldBeEnabled(subscription.Customer))
        {
            return null;
        }

        var options = new SubscriptionUpdateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = ShouldBeEnabled(subscription.Customer),
            },
            DefaultTaxRates = []
        };

        return options;
    }

    private static bool ShouldBeEnabled(Customer customer)
    {
        return customer.HasTaxLocationVerified();
    }
}
