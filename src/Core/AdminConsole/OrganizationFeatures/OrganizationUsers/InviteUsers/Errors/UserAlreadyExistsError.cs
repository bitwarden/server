using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;

public record UserAlreadyExistsError(ScimInviteOrganizationUsersResponse Response) : Error<ScimInviteOrganizationUsersResponse>(Code, Response)
{
    public const string Code = "User already exists";
}
