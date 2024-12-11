#nullable enable

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteManagedOrganizationUserAccountCommand
{
    /// <summary>
    /// Removes a user from an organization and deletes all of their associated user data.
    /// </summary>
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);

    /// <summary>
    /// Removes multiple users from an organization and deletes all of their associated user data.
    /// </summary>
    /// <returns>
    /// An error message for each user that could not be removed, otherwise null.
    /// </returns>
    Task<IEnumerable<(Guid OrganizationUserId, string? ErrorMessage)>> DeleteManyUsersAsync(
        Guid organizationId,
        IEnumerable<Guid> orgUserIds,
        Guid? deletingUserId
    );
}
