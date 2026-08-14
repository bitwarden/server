#nullable enable

namespace Bit.Api.AdminConsole.Authorization.Groups;

/// <summary>
/// Decides whether the caller may update a group's member list and collection access.
/// </summary>
public interface IGroupsAuthorizationService
{
    Task<GroupsAuthorizationResult> AuthorizeUpdateAsync(
        Guid organizationId,
        Guid groupId,
        IReadOnlyCollection<Guid> postedCollectionIds,
        IReadOnlyCollection<Guid> postedUserIds);
}
