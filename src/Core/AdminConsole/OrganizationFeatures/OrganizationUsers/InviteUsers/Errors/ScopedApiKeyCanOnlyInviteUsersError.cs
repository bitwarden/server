using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.Errors;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;

public record ScopedApiKeyCanOnlyInviteUsersError(InviteOrganizationUsersResponse Response) : Error<InviteOrganizationUsersResponse>(Code, Response)
{
    public const string Code = "Scoped organization API keys can only invite members with the User role.";
}
