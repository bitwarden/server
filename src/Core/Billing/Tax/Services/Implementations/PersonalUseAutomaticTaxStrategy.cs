#nullable enable
using Bit.Core.Billing.Extensions;
using Bit.Core.Services;
using Stripe;

namespace Bit.Core.Billing.Tax.Services.Implementations;

public class PersonalUseAutomaticTaxStrategy(IFeatureService featureService) : IAutomaticTaxStrategy
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
        if (!featureService.IsEnabled(FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
        {
            return;
        }
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = ShouldBeEnabled(subscription.Customer)
        };
        options.DefaultTaxRates = [];
    }

    public SubscriptionUpdateOptions? GetUpdateOptions(Subscription subscription)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
        {
            return null;
        }

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

    public void SetInvoiceCreatePreviewOptions(InvoiceCreatePreviewOptions options)
    {
        options.AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true };
    }

    private static bool ShouldBeEnabled(Customer customer)
    {
        return customer.HasRecognizedTaxLocation();
    }
}
