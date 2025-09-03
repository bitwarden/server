using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class UpcomingInvoiceOptionsExtensions
{
    /// <summary>
    /// Attempts to enable automatic tax for given upcoming invoice options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="customer">The existing customer to which the upcoming invoice belongs.</param>
    /// <param name="subscription">The existing subscription to which the upcoming invoice belongs.</param>
    /// <returns>Returns true when successful, false when conditions are not met.</returns>
    public static bool EnableAutomaticTax(
        this UpcomingInvoiceOptions options,
        Customer customer,
        Subscription subscription)
    {
        if (subscription != null && subscription.AutomaticTax.Enabled)
        {
            return false;
        }

        // We might only need to check the automatic tax status.
        if (!customer.HasRecognizedTaxLocation() && string.IsNullOrWhiteSpace(customer.Address?.Country))
        {
            return false;
        }

        options.AutomaticTax = new InvoiceAutomaticTaxOptions { Enabled = true };
        options.SubscriptionDefaultTaxRates = [];

        return true;
    }
}
