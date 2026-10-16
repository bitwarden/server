namespace Bit.Api.AdminConsole.Authorization.Groups;

/// <summary>
/// The authorization decision for saving a group.
/// </summary>
/// <param name="CanAddSelfToGroup">
/// False if the caller is adding themselves to the group and is not allowed to.
/// </param>
/// <param name="UnauthorizedPostedCollectionIds">
/// Posted collections the caller cannot give the group access to. Collections that do not exist, or that belong to
/// another organization, are also listed here.
/// </param>
/// <param name="UnauthorizedCurrentCollectionIds">
/// Collections the group already has access to that the caller cannot change. Save these unchanged, so that the
/// caller does not remove access they cannot see.
/// </param>
public record GroupsAuthorizationResult(
    bool CanAddSelfToGroup,
    IReadOnlySet<Guid> UnauthorizedPostedCollectionIds,
    IReadOnlySet<Guid> UnauthorizedCurrentCollectionIds);
