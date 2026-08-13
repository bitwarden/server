#nullable enable

namespace Bit.Api.AdminConsole.Authorization.OrganizationUsers;

/// <summary>
/// Decides whether the caller may update a single organization user - their collection access and, separately,
/// their group memberships - in one call. Backs <c>OrganizationUsersController</c>'s single-user-scoped Update
/// operation.
/// </summary>
public interface IOrganizationUserAuthorizationService
{
    Task<OrganizationUserAuthorizationResult> AuthorizeUpdateAsync(
        Guid organizationId,
        Guid organizationUserId,
        IReadOnlyCollection<Guid> postedCollectionIds);
}
