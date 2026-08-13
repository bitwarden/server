#nullable enable

namespace Bit.Api.AdminConsole.Authorization.Groups;

/// <summary>
/// The outcome of an <see cref="IGroupsAuthorizationService"/> check. <see cref="UnauthorizedPostedCollectionIds"/>
/// rejects the whole request; <see cref="ReadonlyCurrentCollectionIds"/> identifies the group's existing
/// collection access the caller can't edit, so it can be preserved instead of overwritten.
/// </summary>
public record GroupsAuthorizationResult(
    bool CanAddSelfToGroup,
    IReadOnlySet<Guid> UnauthorizedPostedCollectionIds,
    IReadOnlySet<Guid> ReadonlyCurrentCollectionIds)
{
    public bool IsSuccess => CanAddSelfToGroup && UnauthorizedPostedCollectionIds.Count == 0;
}
