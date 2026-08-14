#nullable enable

namespace Bit.Api.AdminConsole.Authorization.OrganizationUsers;

/// <summary>
/// The outcome of an <see cref="IOrganizationUserAuthorizationService"/> check. <see cref="CanEditOwnGroups"/> is
/// deliberately not part of <see cref="IsSuccess"/> - it tells the caller whether to drop the posted group changes.
/// </summary>
public record OrganizationUserAuthorizationResult(
    bool CanAddSelfToCollection,
    bool CanEditOwnGroups,
    IReadOnlySet<Guid> UnauthorizedPostedCollectionIds,
    IReadOnlySet<Guid> ReadonlyCurrentCollectionIds)
{
    public bool IsSuccess => CanAddSelfToCollection && UnauthorizedPostedCollectionIds.Count == 0;
}
