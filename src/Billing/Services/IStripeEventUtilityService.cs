using Stripe;
using Transaction = Bit.Core.Entities.Transaction;
namespace Bit.Billing.Services;

public interface IStripeEventUtilityService
{
    /// <summary>
    /// Gets the organization or user ID from the metadata of a Stripe Charge object.
    /// </summary>
    /// <param name="charge"></param>
    /// <returns></returns>
    Task<(Guid?, Guid?, Guid?)> GetEntityIdsFromChargeAsync(Charge charge);

    /// <summary>
    /// Gets the organizationId, userId, or providerId from the metadata of a Stripe Subscription object.
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    Tuple<Guid?, Guid?, Guid?> GetIdsFromMetadata(Dictionary<string, string> metadata);

    /// <summary>
    /// Determines whether the specified subscription is a sponsored subscription.
    /// </summary>
    /// <param name="subscription">The subscription to be evaluated.</param>
    /// <returns>
    /// A boolean value indicating whether the subscription is a sponsored subscription.
    /// Returns <c>true</c> if the subscription matches any of the sponsored plans; otherwise, <c>false</c>.
    /// </returns>
    bool IsSponsoredSubscription(Subscription subscription);

    /// <summary>
    /// Converts a Stripe Charge object to a Bitwarden Transaction object.
    /// </summary>
    /// <param name="charge"></param>
    /// <param name="organizationId"></param>
    /// <param name="userId"></param>
    /// /// <param name="providerId"></param>
    /// <returns></returns>
    Transaction FromChargeToTransaction(Charge charge, Guid? organizationId, Guid? userId, Guid? providerId);

    /// <summary>
    /// Attempts to pay the specified invoice. If a customer is eligible, the invoice is paid using Braintree or Stripe.
    /// </summary>
    /// <param name="invoice">The invoice to be paid.</param>
    /// <param name="attemptToPayWithStripe">Indicates whether to attempt payment with Stripe. Defaults to false.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a boolean value indicating whether the invoice payment attempt was successful.</returns>
    Task<bool> AttemptToPayInvoiceAsync(Invoice invoice, bool attemptToPayWithStripe = false);


    /// <summary>
    /// Determines whether an invoice should be attempted to be paid based on certain criteria.
    /// </summary>
    /// <param name="invoice">The invoice to be evaluated.</param>
    /// <returns>A boolean value indicating whether the invoice should be attempted to be paid.</returns>
    bool ShouldAttemptToPayInvoice(Invoice invoice);

    /// <summary>
    /// The ID for the premium annual plan.
    /// </summary>
    const string PremiumPlanId = "premium-annually";

    /// <summary>
    /// The ID for the premium annual plan via the App Store.
    /// </summary>
    const string PremiumPlanIdAppStore = "premium-annually-app";

}
