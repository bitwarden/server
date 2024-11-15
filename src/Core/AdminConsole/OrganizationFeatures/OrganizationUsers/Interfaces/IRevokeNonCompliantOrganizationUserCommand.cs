using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.Models.Commands;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IRevokeNonCompliantOrganizationUserCommand
{
    Task<CommandResult> RevokeNonCompliantOrganizationUsersAsync(RevokeOrganizationUsersRequest request);
}
