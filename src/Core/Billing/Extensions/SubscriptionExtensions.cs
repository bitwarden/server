using Bit.Core.Billing.Constants;
using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class SubscriptionExtensions
{
    public static bool IsOrganization(this Subscription subscription)
    {
        return subscription.Metadata.ContainsKey(StripeConstants.MetadataKeys.OrganizationId);
    }
}
