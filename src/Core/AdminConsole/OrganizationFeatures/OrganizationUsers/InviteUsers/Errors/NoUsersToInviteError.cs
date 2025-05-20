using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.Errors;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;

public record NoUsersToInviteError(InviteOrganizationUsersResponse Response) : Error<InviteOrganizationUsersResponse>(Code, Response)
{
    public const string Code = "No users to invite";
}
