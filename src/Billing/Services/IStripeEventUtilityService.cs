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

    Task<bool> AttemptToPayInvoiceAsync(Invoice invoice, bool attemptToPayWithStripe = false);

    bool ShouldAttemptToPayInvoice(Invoice invoice);
}
