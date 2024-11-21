using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IRemoveOrganizationUserCommand
{
    /// <summary>
    /// Removes a user from an organization.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="userId">The ID of the user to remove.</param>
    Task RemoveUserAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Removes a user from an organization with a specified deleting user.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserId">The ID of the organization user to remove.</param>
    /// <param name="deletingUserId">The ID of the user performing the removal operation.</param>
    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);

    /// <summary>
    /// Removes a user from an organization using a system user.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserId">The ID of the organization user to remove.</param>
    /// <param name="eventSystemUser">The system user performing the removal operation.</param>
    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser);

    /// <summary>
    /// Removes multiple users from an organization with a specified deleting user.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserIds">The collection of organization user IDs to remove.</param>
    /// <param name="deletingUserId">The ID of the user performing the removal operation.</param>
    /// <returns>
    /// A list of tuples containing the organization user id and the error message for each user that could not be removed, otherwise empty.
    /// </returns>
    Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> RemoveUsersAsync(
        Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? deletingUserId);

    /// <summary>
    /// Removes multiple users from an organization using a system user.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserIds">The collection of organization user IDs to remove.</param>
    /// <param name="eventSystemUser">The system user performing the removal operation.</param>
    /// <returns>
    /// A list of tuples containing the organization user id and the error message for each user that could not be removed, otherwise empty.
    /// </returns>
    Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> RemoveUsersAsync(
        Guid organizationId, IEnumerable<Guid> organizationUserIds, EventSystemUser eventSystemUser);
}
