#nullable enable

namespace Bit.Api.AdminConsole.Authorization.OrganizationUsers;

/// <summary>
/// The outcome of an <see cref="IOrganizationUserAuthorizationService"/> check. <see cref="UnauthorizedPostedCollectionIds"/>
/// rejects the whole request; <see cref="ReadonlyCurrentCollectionIds"/> identifies the target's existing
/// collection access the caller can't edit, so it can be preserved instead of overwritten.
/// <see cref="CanEditOwnGroups"/> is not part of <see cref="IsSuccess"/> - unlike the collection checks, it
/// doesn't reject the request, it tells the caller whether to drop the posted group changes silently.
/// </summary>
public record OrganizationUserAuthorizationResult(
    bool CanAddSelfToCollection,
    bool CanEditOwnGroups,
    IReadOnlySet<Guid> UnauthorizedPostedCollectionIds,
    IReadOnlySet<Guid> ReadonlyCurrentCollectionIds)
{
    public bool IsSuccess => CanAddSelfToCollection && UnauthorizedPostedCollectionIds.Count == 0;
}
