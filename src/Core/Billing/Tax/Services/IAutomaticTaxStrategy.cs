#nullable enable
using Stripe;

namespace Bit.Core.Billing.Tax.Services;

public interface IAutomaticTaxStrategy
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="subscription"></param>
    /// <returns>
    /// Returns <see cref="SubscriptionUpdateOptions" /> if changes are to be applied to the subscription, returns null
    /// otherwise.
    /// </returns>
    SubscriptionUpdateOptions? GetUpdateOptions(Subscription subscription);

    /// <summary>
    /// Modifies an existing <see cref="SubscriptionCreateOptions" /> object with the automatic tax flag set correctly.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="customer"></param>
    void SetCreateOptions(SubscriptionCreateOptions options, Customer customer);

    /// <summary>
    /// Modifies an existing <see cref="SubscriptionUpdateOptions" /> object with the automatic tax flag set correctly.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="subscription"></param>
    void SetUpdateOptions(SubscriptionUpdateOptions options, Subscription subscription);

    void SetInvoiceCreatePreviewOptions(InvoiceCreatePreviewOptions options);
}
