using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public interface IUpdateOrganizationUserCommand
{
    Task<CommandResult> UpdateUserAsync(UpdateOrganizationUserRequest request);
}
