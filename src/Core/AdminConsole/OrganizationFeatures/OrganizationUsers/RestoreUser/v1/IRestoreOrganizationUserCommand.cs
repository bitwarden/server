using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;

/// <summary>
/// Restores a user back to their previous status.
/// </summary>
public interface IRestoreOrganizationUserCommand
{
    /// <summary>
    /// Validates that the requesting user can perform the action. There is also a check done to ensure the organization
    /// can re-add this user based on their current occupied seats.
    ///
    /// Checks are performed to make sure the user is conforming to all policies enforced by the organization as well as
    /// other organizations the user may belong to.
    ///
    /// Reference Events and Push Notifications are fired off for this as well.
    /// </summary>
    /// <param name="organizationUser">Revoked user to be restored.</param>
    /// <param name="restoringUserId">UserId of the user performing the action.</param>
    Task RestoreUserAsync(OrganizationUser organizationUser, Guid? restoringUserId);

    /// <summary>
    /// Validates that the requesting user can perform the action. There is also a check done to ensure the organization
    /// can re-add this user based on their current occupied seats.
    ///
    /// Checks are performed to make sure the user is conforming to all policies enforced by the organization as well as
    /// other organizations the user may belong to.
    ///
    /// Reference Events and Push Notifications are fired off for this as well.
    /// </summary>
    /// <param name="organizationUser">Revoked user to be restored.</param>
    /// <param name="systemUser">System that is performing the action on behalf of the organization (Public API, SCIM, etc.)</param>
    Task RestoreUserAsync(OrganizationUser organizationUser, EventSystemUser systemUser);

    /// <summary>
    /// Validates that the requesting user can perform the action. There is also a check done to ensure the organization
    /// can re-add this user based on their current occupied seats.
    ///
    /// Checks are performed to make sure the user is conforming to all policies enforced by the organization as well as
    /// other organizations the user may belong to.
    ///
    /// Reference Events and Push Notifications are fired off for this as well.
    /// </summary>
    /// <param name="organizationId">Organization the users should be restored to.</param>
    /// <param name="organizationUserIds">List of organization user ids to restore to previous status.</param>
    /// <param name="restoringUserId">UserId of the user performing the action.</param>
    /// <param name="userService">Passed in from caller to avoid circular dependency</param>
    /// <returns>List of organization user Ids and strings. A successful restoration will have an empty string.
    /// If an error occurs, the error message will be provided.</returns>
    Task<List<Tuple<OrganizationUser, string>>> RestoreUsersAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? restoringUserId, IUserService userService);
}
