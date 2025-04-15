using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Payments;

public record PaymentCancelledSubscriptionError(PaymentsSubscription InvalidRequest)
    : Error<PaymentsSubscription>(Code, InvalidRequest)
{
    public const string Code = "You do not have an active subscription. Reinstate your subscription to make changes.";
}
