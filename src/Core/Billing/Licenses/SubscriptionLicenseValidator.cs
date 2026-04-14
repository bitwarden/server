using Bit.Core.Billing.Constants;
using Bit.Core.Exceptions;
using Stripe;

namespace Bit.Core.Billing.Licenses;

public static class SubscriptionLicenseValidator
{
    public static void ValidateSubscriptionForLicenseGeneration(Subscription? subscription)
    {
        if (subscription == null)
        {
            throw new BadRequestException("No active subscription found.");
        }

        var status = subscription.Status;

        if (status is StripeConstants.SubscriptionStatus.Canceled or StripeConstants.SubscriptionStatus.Incomplete
            or StripeConstants.SubscriptionStatus.IncompleteExpired)
        {
            throw new BadRequestException(
                "Unable to generate license due to a payment issue. Please update your billing information or contact support for assistance.");
        }
    }
}
