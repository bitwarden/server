using Bit.Core.Billing.Constants;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations.AutomaticTax;

public class IndividualAutomaticTaxStrategy : IIndividualAutomaticTaxStrategy
{
    public Task SetCreateOptionsAsync(SubscriptionCreateOptions options, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(customer);

        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = ShouldEnable(customer)
        };

        return Task.CompletedTask;
    }

    public Task SetUpdateOptionsAsync(SubscriptionUpdateOptions options, Subscription subscription)
    {

        ArgumentNullException.ThrowIfNull(options);

        if (subscription.AutomaticTax.Enabled == ShouldEnable(subscription.Customer))
        {
            return Task.CompletedTask;
        }

        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = ShouldEnable(subscription.Customer)
        };
        options.DefaultTaxRates = [];

        return Task.CompletedTask;
    }

    public Task<SubscriptionUpdateOptions> GetUpdateOptionsAsync(Subscription subscription)
    {
        if (subscription.AutomaticTax.Enabled == ShouldEnable(subscription.Customer))
        {
            return null;
        }

        var options = new SubscriptionUpdateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = ShouldEnable(subscription.Customer),
            },
            DefaultTaxRates = []
        };

        return Task.FromResult(options);
    }

    private static bool ShouldEnable(Customer customer)
    {
        return customer.Tax?.AutomaticTax == StripeConstants.AutomaticTaxStatus.Supported;
    }
}
