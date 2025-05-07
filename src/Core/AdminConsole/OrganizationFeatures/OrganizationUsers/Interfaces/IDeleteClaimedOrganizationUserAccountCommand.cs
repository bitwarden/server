#nullable enable

using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteClaimedOrganizationUserAccountCommand
{
    /// <summary>
    /// Removes a user from an organization and deletes all of their associated user data.
    /// </summary>
    Task<CommandResult<DeleteUserResponse>> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId);

    /// <summary>
    /// Removes multiple users from an organization and deletes all of their associated user data.
    /// </summary>
    /// <returns>
    /// An error message for each user that could not be removed, otherwise null.
    /// </returns>
    Task<Partial<DeleteUserResponse>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid deletingUserId);
}
