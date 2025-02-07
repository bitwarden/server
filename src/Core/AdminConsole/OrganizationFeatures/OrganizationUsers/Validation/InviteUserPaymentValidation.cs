using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

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

public record PaymentSubscriptionDto
{
    public ProductTierType ProductTierType { get; init; }
    public string SubscriptionStatus { get; init; }

    public static PaymentSubscriptionDto FromSubscriptionInfo(SubscriptionInfo subscriptionInfo, OrganizationDto organizationDto) =>
        new()
        {
            SubscriptionStatus = subscriptionInfo?.Subscription?.Status ?? string.Empty,
            ProductTierType = organizationDto.Plan.ProductTier
        };
}

