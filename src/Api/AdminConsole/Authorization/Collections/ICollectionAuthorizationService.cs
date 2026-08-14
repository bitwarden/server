namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Decides whether the caller may update a collection's metadata, or a collection's user or group access,
/// each as an independent operation.
/// </summary>
public interface ICollectionAuthorizationService
{
    /// <summary>
    /// Determines whether the caller may update a collection's metadata.
    /// </summary>
    /// <param name="organizationId">The ID of the organization the collection belongs to.</param>
    /// <param name="collectionId">The ID of the collection to update.</param>
    /// <returns>True if the caller may update the collection's metadata, false otherwise.</returns>
    Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId);

    /// <summary>
    /// Determines whether the caller may modify a collection's user access.
    /// </summary>
    /// <param name="organizationId">The ID of the organization the collection belongs to.</param>
    /// <param name="collectionId">The ID of the collection to modify user access for.</param>
    /// <returns>True if the caller may modify the collection's user access, false otherwise.</returns>
    Task<bool> AuthorizeModifyUserAccessAsync(Guid organizationId, Guid collectionId);

    /// <summary>
    /// Determines whether the caller may modify a collection's group access.
    /// </summary>
    /// <param name="organizationId">The ID of the organization the collection belongs to.</param>
    /// <param name="collectionId">The ID of the collection to modify group access for.</param>
    /// <returns>True if the caller may modify the collection's group access, false otherwise.</returns>
    Task<bool> AuthorizeModifyGroupAccessAsync(Guid organizationId, Guid collectionId);
}
