using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Models;
using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IGetOrganizationUserQuery
{
    /// <summary>
    /// Retrieves an organization user by their ID and returns the appropriate strongly-typed model
    /// based on their status (Invited, Accepted, Confirmed, or Revoked).
    /// </summary>
    /// <param name="organizationUserId">The ID of the organization user to retrieve.</param>
    Task<ITypedOrganizationUser?> GetOrganizationUserAsync(Guid organizationUserId);

    /// <summary>
    /// Retrieves multiple organization users by their IDs and returns the appropriate strongly-typed models
    /// based on their status (Invited, Accepted, Confirmed, or Revoked).
    /// </summary>
    /// <param name="organizationUserIds">The IDs of the organization users to retrieve.</param>
    Task<IEnumerable<ITypedOrganizationUser>> GetManyOrganizationUsersAsync(IEnumerable<Guid> organizationUserIds);
}
