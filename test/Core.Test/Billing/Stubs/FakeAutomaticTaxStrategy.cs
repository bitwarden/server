﻿using Bit.Core.Billing.Services;
using Stripe;

namespace Bit.Core.Test.Billing.Stubs;

/// <param name="isAutomaticTaxEnabled">
/// Whether the subscription options will have automatic tax enabled or not.
/// </param>
public class FakeAutomaticTaxStrategy(
    bool isAutomaticTaxEnabled) : IAutomaticTaxStrategy
{
    public SubscriptionUpdateOptions? GetUpdateOptions(Subscription subscription)
    {
        return new SubscriptionUpdateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = isAutomaticTaxEnabled }
        };
    }

    public void SetCreateOptions(SubscriptionCreateOptions options, Customer customer)
    {
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = isAutomaticTaxEnabled };
    }

    public void SetUpdateOptions(SubscriptionUpdateOptions options, Subscription subscription)
    {
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = isAutomaticTaxEnabled };
    }

    public void SetInvoiceCreatePreviewOptions(InvoiceCreatePreviewOptions options)
    {
        options.AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = isAutomaticTaxEnabled };

    }
}
