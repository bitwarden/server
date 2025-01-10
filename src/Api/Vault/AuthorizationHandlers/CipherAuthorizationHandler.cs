using Bit.Api.Vault.Authorization;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers;

// Example of how the functional logic can be reused here
public class CipherAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Cipher>
{
    private readonly ICipherRepository _cipherRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICurrentContext _currentContext;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        Cipher resource)
    {
        // Assume a CanDelete requirement here
        if (await CanDelete(resource))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanDelete(Cipher resource)
    {
        // Get state - ideally this would be encapsulated a bit better
        // but the point is that it's separate from the underlying functional logic
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(resource.Id);
        var collectionCiphers = cipher.OrganizationId.HasValue
            ? await _collectionCipherRepository.GetManyByOrganizationIdAsync(cipher.OrganizationId.Value)
            : new List<CollectionCipher>();

        var collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);

        var cipherWithCollectionIds = new CipherDetailsWithCollections(cipher, collectionCiphersGroupDict);

        var allCollections = cipher.OrganizationId.HasValue
            ? await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value)
            : new List<CollectionDetails>();

        // Give it to the same underlying function as the sync data
        // Note that by this point we've fetched all our data upfront, we could map this over a list of many ciphers if we wanted
        // without any performance concerns
        return ItemPermissionHelpers.CanDelete(_currentContext.UserId.Value, cipherWithCollectionIds, allCollections);
    }
}
