namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Decides whether the caller may update a collection's metadata, user access, and group access.
/// </summary>
public interface ICollectionAuthorizationService
{
    Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId);
}
