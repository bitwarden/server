using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public interface IRevokeOrganizationUserCommand
{
    Task<IEnumerable<BulkCommandResult>> RevokeUsersAsync(RevokeOrganizationUsersRequest request);
}
