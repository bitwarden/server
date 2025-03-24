#nullable enable
using Bit.Core.Billing.Extensions;
using Bit.Core.Services;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations.AutomaticTax;

public class BusinessUseAutomaticTaxStrategy(IFeatureService featureService) : IAutomaticTaxStrategy
{
    public SubscriptionUpdateOptions? GetUpdateOptions(Subscription subscription)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
        {
            return null;
        }

        var shouldBeEnabled = ShouldBeEnabled(subscription.Customer);
        if (subscription.AutomaticTax.Enabled == shouldBeEnabled)
        {
            return null;
        }

        var options = new SubscriptionUpdateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = shouldBeEnabled
            },
            DefaultTaxRates = []
        };

        return options;
    }

    public void SetCreateOptions(SubscriptionCreateOptions options, Customer customer)
    {
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = ShouldBeEnabled(customer)
        };
    }

    public void SetUpdateOptions(SubscriptionUpdateOptions options, Subscription subscription)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
        {
            return;
        }

        var shouldBeEnabled = ShouldBeEnabled(subscription.Customer);

        if (subscription.AutomaticTax.Enabled == shouldBeEnabled)
        {
            return;
        }

        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = shouldBeEnabled
        };
        options.DefaultTaxRates = [];
    }

    private bool ShouldBeEnabled(Customer customer)
    {
        if (!customer.HasTaxLocationVerified())
        {
            return false;
        }

        if (customer.Address.Country == "US")
        {
            return true;
        }

        return customer.TaxIds != null && customer.TaxIds.Any();
    }
}
