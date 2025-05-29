using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Payments;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class InviteUserPaymentValidation
{
    public static ValidationResult<PaymentsSubscription> Validate(PaymentsSubscription subscription)
    {
        if (subscription.ProductTierType is ProductTierType.Free)
        {
            return new Valid<PaymentsSubscription>(subscription);
        }

        if (subscription.SubscriptionStatus == StripeConstants.SubscriptionStatus.Canceled)
        {
            return new Invalid<PaymentsSubscription>(new PaymentCancelledSubscriptionError(subscription));
        }

        return new Valid<PaymentsSubscription>(subscription);
    }
}
