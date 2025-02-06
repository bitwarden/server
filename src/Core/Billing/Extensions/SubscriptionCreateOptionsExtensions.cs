using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class SubscriptionCreateOptionsExtensions
{
    /// <summary>
    /// Attempts to enable automatic tax for given new subscription options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="customer">The existing customer.</param>
    /// <returns>Returns true when successful, false when conditions are not met.</returns>
    public static bool EnableAutomaticTax(this SubscriptionCreateOptions options, Customer customer)
    {
        // We might only need to check the automatic tax status.
        if (!customer.HasTaxLocationVerified() && string.IsNullOrWhiteSpace(customer.Address?.Country))
        {
            return false;
        }

        options.DefaultTaxRates = [];
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true };

        return true;
    }
}
