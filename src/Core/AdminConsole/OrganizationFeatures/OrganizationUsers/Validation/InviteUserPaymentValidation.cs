using Bit.Core.Billing.Constants;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public static class InviteUserPaymentValidation
{
    public static ValidationResult<PaymentSubscriptionDto> Validate(PaymentSubscriptionDto subscription)
    {
        if (subscription.SubscriptionStatus == StripeConstants.SubscriptionStatus.Canceled)
        {
            return new Invalid<PaymentSubscriptionDto>(InviteUserValidationErrorMessages.CancelledSubscriptionError);
        }

        return new Valid<PaymentSubscriptionDto>(subscription);
    }
}

public record PaymentSubscriptionDto
{
    public string SubscriptionStatus { get; init; }

    public static PaymentSubscriptionDto FromSubscriptionInfo(SubscriptionInfo subscriptionInfo) =>
        new()
        {
            SubscriptionStatus = subscriptionInfo?.Subscription?.Status ?? string.Empty
        };
}

