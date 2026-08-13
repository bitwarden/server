#nullable enable

namespace Bit.Api.AdminConsole.Authorization.Groups;

/// <summary>
/// Decides whether the caller may update a single group - its member list and its collection access - in one
/// call. Backs <c>GroupsController</c>'s single-group-scoped Update operation.
/// </summary>
public interface IGroupsAuthorizationService
{
    Task<GroupsAuthorizationResult> AuthorizeUpdateAsync(
        Guid organizationId,
        Guid groupId,
        IReadOnlyCollection<Guid> postedCollectionIds,
        IReadOnlyCollection<Guid> postedUserIds);
}
