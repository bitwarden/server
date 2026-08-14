#nullable enable

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Decides whether the caller may update a single collection - its own metadata, its user access, and its
/// group access - in one call. Backs <c>CollectionsController</c>'s single-collection-scoped Update operation.
/// </summary>
public interface ICollectionAuthorizationService
{
    Task<CollectionAuthorizationResult> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId);
}
