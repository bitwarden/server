using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Billing.Queries;

public interface IGetSubscriptionQuery
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="subscriber"></param>
    /// <returns></returns>
    Task<Subscription> GetSubscription(ISubscriber subscriber);
}
