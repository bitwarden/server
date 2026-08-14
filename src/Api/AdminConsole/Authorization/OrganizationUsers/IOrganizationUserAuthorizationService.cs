#nullable enable

namespace Bit.Api.AdminConsole.Authorization.OrganizationUsers;

/// <summary>
/// Decides whether the caller may update an organization user's collection access and group memberships.
/// </summary>
public interface IOrganizationUserAuthorizationService
{
    Task<OrganizationUserAuthorizationResult> AuthorizeUpdateAsync(
        Guid organizationId,
        Guid organizationUserId,
        IReadOnlyCollection<Guid> postedCollectionIds);
}
