using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;

/// <summary>
/// Allows users to revoke themselves from an organization when declining to migrate personal items
/// under the OrganizationDataOwnership policy.
/// </summary>
public interface ISelfRevokeOrganizationUserCommand
{
    /// <summary>
    /// Revokes a user from an organization.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="userId">The user ID to revoke.</param>
    /// <returns>A <see cref="CommandResult"/> indicating success or containing an error.</returns>
    /// <remarks>
    /// Validates the OrganizationDataOwnership policy is enabled and applies to the user (currently Owners/Admins are exempt),
    /// the user is a confirmed member, and prevents the last owner from revoking themselves.
    /// </remarks>
    Task<CommandResult> SelfRevokeUserAsync(Guid organizationId, Guid userId);
}
