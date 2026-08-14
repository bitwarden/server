#nullable enable

namespace Bit.Api.AdminConsole.Authorization.Groups;

/// <summary>
/// The outcome of an <see cref="IGroupsAuthorizationService"/> check.
/// </summary>
public record GroupsAuthorizationResult(
    bool CanAddSelfToGroup,
    IReadOnlySet<Guid> UnauthorizedPostedCollectionIds,
    IReadOnlySet<Guid> ReadonlyCurrentCollectionIds)
{
    public bool IsSuccess => CanAddSelfToGroup && UnauthorizedPostedCollectionIds.Count == 0;
}
