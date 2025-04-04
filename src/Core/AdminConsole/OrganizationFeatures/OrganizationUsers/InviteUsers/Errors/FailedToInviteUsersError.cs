using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;

public record FailedToInviteUsersError(InviteOrganizationUsersResponse Response) : Error<InviteOrganizationUsersResponse>(Code, Response)
{
    public const string Code = "Failed to invite users";
}
