using Stripe;

namespace Bit.Core.Billing.Services.Implementations.AutomaticTax;

public class IndividualAutomaticTaxStrategy : IIndividualAutomaticTaxStrategy
{
    public Task SetCreateOptionsAsync(SubscriptionCreateOptions options, Customer customer = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
        return Task.CompletedTask;
    }

    public Task SetUpdateOptionsAsync(SubscriptionUpdateOptions options, Subscription subscription)
    {

        ArgumentNullException.ThrowIfNull(options);

        if (subscription.AutomaticTax.Enabled)
        {
            return Task.CompletedTask;
        }

        options.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };
        return Task.CompletedTask;
    }

    public Task<SubscriptionUpdateOptions> GetUpdateOptionsAsync(Subscription subscription)
    {
        if (subscription.AutomaticTax.Enabled)
        {
            return null;
        }

        var options = new SubscriptionUpdateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = true
            }
        };

        return Task.FromResult(options);
    }
}
