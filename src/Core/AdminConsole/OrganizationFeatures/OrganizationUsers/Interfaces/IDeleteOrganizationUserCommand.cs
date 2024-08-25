using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteOrganizationUserCommand
{
    /// <summary>
    /// Deletes a single user from an organization.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserId">The ID of the organization user to delete.</param>
    /// <param name="deletingUserId">The ID of the user performing the deletion, if applicable.</param>
    /// <param name="removalType">The type of removal being performed.</param>
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId, OrganizationUserRemovalType removalType = OrganizationUserRemovalType.AdminRemoved);

    /// <summary>
    /// Deletes multiple users from an organization.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserIds">The IDs of the organization users to delete.</param>
    /// <param name="deletingUserId">The ID of the user performing the deletion, if applicable.</param>
    /// <param name="removalType">The type of removal being performed.</param>
    Task DeleteUsersAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? deletingUserId, OrganizationUserRemovalType removalType = OrganizationUserRemovalType.AdminRemoved);

    /// <summary>
    /// Deletes a single user from an organization using a system user. This method is intended for automated requests.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="organizationUserId">The ID of the organization user to delete.</param>
    /// <param name="eventSystemUser">The system user performing the deletion.</param>
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser);
}


