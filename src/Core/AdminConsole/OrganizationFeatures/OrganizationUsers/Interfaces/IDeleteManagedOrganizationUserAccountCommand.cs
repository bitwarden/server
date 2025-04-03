#nullable enable

using Bit.Core.Models.Commands;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteManagedOrganizationUserAccountCommand
{
    /// <summary>
    /// Removes a user from an organization and deletes all of their associated user data.
    /// </summary>
    ///  Jimmy temporary comment: consider removing the nullable from deletingUserId.
    Task<CommandResult> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);

    /// <summary>
    /// Removes multiple users from an organization and deletes all of their associated user data.
    /// </summary>
    /// <returns>
    /// An error message for each user that could not be removed, otherwise null.
    /// </returns>
    /// Jimmy temporary comment: consider removing the nullable from deletingUserId.
    Task<IEnumerable<(Guid OrganizationUserId, CommandResult result)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId);
}
