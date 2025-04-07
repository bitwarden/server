using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;

public record OrganizationNoPaymentMethodFoundError(InviteOrganization InvalidRequest)
    : Error<InviteOrganization>(Code, InvalidRequest)
{
    public const string Code = "No payment method found.";
}

public record OrganizationNoSubscriptionFoundError(InviteOrganization InvalidRequest)
    : Error<InviteOrganization>(Code, InvalidRequest)
{
    public const string Code = "No subscription found.";
}
