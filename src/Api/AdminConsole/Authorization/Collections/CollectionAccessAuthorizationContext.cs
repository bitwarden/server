namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Caller and organization data fetched once per request and shared across collection authorization checks.
/// </summary>
public readonly record struct CollectionAccessAuthorizationContext(
    bool AllowAdminAccessToAllCollectionItems,
    bool CallerIsProviderUser,
    HashSet<Guid> CallerManagedCollectionIds,
    HashSet<Guid> OrphanedCollectionIds,
    Guid? CallerOrganizationUserId = null);
