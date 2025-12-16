namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;

/// <summary>
/// Allows users to revoke themselves from an organization when declining to migrate personal items
/// under the OrganizationDataOwnership policy.
/// </summary>
public interface ISelfRevokeOrganizationUserCommand
{
    /// <summary>
    /// Revokes a user from an organization. Validates policy is enabled and user is eligible (not Owner/Admin).
    /// </summary>
    Task SelfRevokeUserAsync(Guid organizationId, Guid userId);
}
