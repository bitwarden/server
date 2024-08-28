using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

/// <summary>
/// Remove users from an organization.
/// </summary>
public interface IRemoveOrganizationUserCommand
{
    /// <summary>
    /// Removes a user from an organization manually, initiated by another user.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserId">The ID of the user to be removed from the organization.</param>
    /// <param name="deletingUserId">The ID of the user initiating the removal (optional).</param>
    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);

    /// <summary>
    /// Removes a user from an organization automatically, initiated by a system process.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserId">The ID of the user to be removed from the organization.</param>
    /// <param name="eventSystemUser">The type of system user initiating the removal.</param>
    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser);
}
