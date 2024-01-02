using Bit.Core.Utilities;
using Stripe;

namespace Bit.Billing.Services;

public interface IWebhookUtility
{
    Task<bool> AttemptToPayInvoice(Invoice invoice, bool attemptToPayWithStripe = false);
    Tuple<Guid?, Guid?> GetIdsFromMetaData(IDictionary<string, string> metaData);

    bool IsSponsoredSubscription(Subscription subscription) =>
        StaticStore.SponsoredPlans.Any(p => p.StripePlanId == subscription.Id);

    bool UnpaidAutoChargeInvoiceForSubscriptionCycle(Invoice invoice);
}
