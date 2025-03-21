using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Payments;

public record PaymentCancelledSubscriptionError(PaymentsSubscription InvalidRequest)
    : Error<PaymentsSubscription>(Code, InvalidRequest)
{
    public const string Code = "Cannot autoscale with a canceled subscription.";
}
