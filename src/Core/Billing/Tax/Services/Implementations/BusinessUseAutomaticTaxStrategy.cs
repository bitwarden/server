#nullable enable
using Bit.Core.Billing.Extensions;
using Bit.Core.Services;
using Stripe;

namespace Bit.Core.Billing.Tax.Services.Implementations;

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

    public void SetInvoiceCreatePreviewOptions(InvoiceCreatePreviewOptions options)
    {
        options.AutomaticTax ??= new InvoiceAutomaticTaxOptions();

        if (options.CustomerDetails.Address.Country == "US")
        {
            options.AutomaticTax.Enabled = true;
            return;
        }

        options.AutomaticTax.Enabled = options.CustomerDetails.TaxIds != null && options.CustomerDetails.TaxIds.Any();
    }

    private bool ShouldBeEnabled(Customer customer)
    {
        if (!customer.HasRecognizedTaxLocation())
        {
            return false;
        }

        if (customer.Address.Country == "US")
        {
            return true;
        }

        if (customer.TaxIds == null)
        {
            throw new ArgumentNullException(nameof(customer.TaxIds), "`customer.tax_ids` must be expanded.");
        }

        return customer.TaxIds.Any();
    }
}
