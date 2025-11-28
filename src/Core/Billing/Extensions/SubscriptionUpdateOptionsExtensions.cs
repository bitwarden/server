using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class SubscriptionUpdateOptionsExtensions
{
    /// <summary>
    /// Attempts to enable automatic tax for given subscription options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="customer">The existing customer to which the subscription belongs.</param>
    /// <param name="subscription">The existing subscription.</param>
    /// <returns>Returns true when successful, false when conditions are not met.</returns>
    public static bool EnableAutomaticTax(
        this SubscriptionUpdateOptions options,
        Customer customer,
        Subscription subscription)
    {
        if (subscription.AutomaticTax.Enabled)
        {
            return false;
        }

        // We might only need to check the automatic tax status.
        if (!customer.HasRecognizedTaxLocation() && string.IsNullOrWhiteSpace(customer.Address?.Country))
        {
            return false;
        }

        options.DefaultTaxRates = [];
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };

        return true;
    }
}
