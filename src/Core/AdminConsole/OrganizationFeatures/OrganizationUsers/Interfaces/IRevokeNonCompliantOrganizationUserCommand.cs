using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IRevokeNonCompliantOrganizationUserCommand
{
    Task<CommandResult> RevokeNonCompliantOrganizationUsersAsync(RevokeOrganizationUsers request);
}
