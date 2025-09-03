using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.Utilities.Errors;

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
