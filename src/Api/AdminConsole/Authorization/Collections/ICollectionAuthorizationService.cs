namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Decides if the caller can update a collection's metadata, its user access, or its group access.
/// Each operation is authorized independently.
/// </summary>
/// <remarks>
/// A collection ID is left out of the result if the collection does not exist, or if it belongs to a different
/// organization. No error is thrown in these cases. An empty request returns an empty result, so do not read an
/// empty result as authorized.
/// </remarks>
public interface ICollectionAuthorizationService
{
    /// <summary>
    /// Determines if the caller can update the collection's metadata (name, externalId).
    /// </summary>
    /// <param name="organizationId">The ID of the organization that owns the collection.</param>
    /// <param name="collectionId">The ID of the collection to update.</param>
    /// <returns>True if the caller can update the collection's metadata, false if not.</returns>
    Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId);

    /// <summary>
    /// Determines which of the requested collections have user access that the caller can modify.
    /// </summary>
    /// <param name="organizationId">The ID of the organization that owns the collections.</param>
    /// <param name="collectionIds">The IDs of the collections to check.</param>
    /// <returns>The subset of <paramref name="collectionIds"/> with user access that the caller can modify.</returns>
    Task<IReadOnlySet<Guid>> AuthorizeModifyUserAccessManyAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds);

    /// <summary>
    /// Determines which of the requested collections have group access that the caller can modify.
    /// </summary>
    /// <param name="organizationId">The ID of the organization that owns the collections.</param>
    /// <param name="collectionIds">The IDs of the collections to check.</param>
    /// <returns>The subset of <paramref name="collectionIds"/> with group access that the caller can modify.</returns>
    Task<IReadOnlySet<Guid>> AuthorizeModifyGroupAccessManyAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds);
}
