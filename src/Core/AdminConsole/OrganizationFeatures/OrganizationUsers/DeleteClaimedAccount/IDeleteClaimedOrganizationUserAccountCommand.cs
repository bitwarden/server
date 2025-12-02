using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

public interface IDeleteClaimedOrganizationUserAccountCommand
{
    /// <summary>
    /// Removes a user from an organization and deletes all of their associated user data.
    /// </summary>
    Task<BulkCommandResult> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId);

    /// <summary>
    /// Removes multiple users from an organization and deletes all of their associated user data.
    /// </summary>
    /// <returns>
    /// An error message for each user that could not be removed, otherwise null.
    /// </returns>
    Task<IEnumerable<BulkCommandResult>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid deletingUserId);
}
