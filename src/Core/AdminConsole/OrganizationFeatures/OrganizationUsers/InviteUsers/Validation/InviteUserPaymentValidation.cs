using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class InviteUserPaymentValidation
{
    public static ValidationResult<PaymentSubscriptionDto> Validate(PaymentSubscriptionDto subscription)
    {
        if (subscription.ProductTierType is ProductTierType.Free)
        {
            return new Valid<PaymentSubscriptionDto>(subscription);
        }

        if (subscription.SubscriptionStatus == StripeConstants.SubscriptionStatus.Canceled)
        {
            return new Invalid<PaymentSubscriptionDto>(InviteUserValidationErrorMessages.CancelledSubscriptionError);
        }

        return new Valid<PaymentSubscriptionDto>(subscription);
    }
}
