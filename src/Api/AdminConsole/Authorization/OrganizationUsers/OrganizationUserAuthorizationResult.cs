namespace Bit.Api.AdminConsole.Authorization.OrganizationUsers;

/// <summary>
/// The authorization decision for saving an organization user.
/// </summary>
/// <param name="CanAddSelfToCollection">
/// False if the caller is giving themselves access to a collection they cannot already reach.
/// </param>
/// <param name="CanEditOwnGroups">
/// False if the caller cannot change their own group membership. Save no group changes in that case.
/// </param>
/// <param name="UnauthorizedPostedCollectionIds">
/// Posted collections the caller cannot give the organization user access to. Collections that do not exist, or
/// that belong to another organization, are also listed here.
/// </param>
/// <param name="UnauthorizedCurrentCollectionIds">
/// Collections the organization user can already reach that the caller cannot change. Save these unchanged, so
/// that the caller does not remove access they cannot see.
/// </param>
public record OrganizationUserAuthorizationResult(
    bool CanAddSelfToCollection,
    bool CanEditOwnGroups,
    IReadOnlySet<Guid> UnauthorizedPostedCollectionIds,
    IReadOnlySet<Guid> UnauthorizedCurrentCollectionIds);
