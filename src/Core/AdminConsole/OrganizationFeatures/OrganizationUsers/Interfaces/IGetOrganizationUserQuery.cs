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
    /// <returns>
    /// A OneOf containing either:
    /// - InvitedOrganizationUser (status: Invited or Revoked-Invited)
    /// - AcceptedOrganizationUser (status: Accepted or Revoked-Accepted)
    /// - ConfirmedOrganizationUser (status: Confirmed or Revoked-Confirmed)
    /// - None if the user is not found
    /// </returns>
    Task<OneOf<InvitedOrganizationUser, AcceptedOrganizationUser, ConfirmedOrganizationUser, None>> GetOrganizationUserAsync(Guid organizationUserId);
}
