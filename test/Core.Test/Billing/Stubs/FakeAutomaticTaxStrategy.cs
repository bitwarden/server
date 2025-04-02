using Bit.Core.Billing.Services;
using Stripe;

namespace Bit.Core.Test.Billing.Stubs;

/// <param name="IsAutomaticTaxEnabled">
/// Whether the subscription options will have automatic tax enabled or not.
/// </param>
public class FakeAutomaticTaxStrategy(
    bool IsAutomaticTaxEnabled) : IAutomaticTaxStrategy
{
    public SubscriptionUpdateOptions? GetUpdateOptions(Subscription subscription)
    {
        return new SubscriptionUpdateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = IsAutomaticTaxEnabled }
        };
    }

    public void SetCreateOptions(SubscriptionCreateOptions options, Customer customer)
    {
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = IsAutomaticTaxEnabled };
    }

    public void SetUpdateOptions(SubscriptionUpdateOptions options, Subscription subscription)
    {
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = IsAutomaticTaxEnabled };
    }

    public void SetInvoiceCreatePreviewOptions(InvoiceCreatePreviewOptions options)
    {
        options.AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = IsAutomaticTaxEnabled };

    }
}
