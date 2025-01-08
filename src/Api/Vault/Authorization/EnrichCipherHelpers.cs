using Bit.Core.Models.Data;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Authorization;


public static class EnrichCipherHelpers
{
    /// <summary>
    /// Enrich a cipher with the user's permissions.
    /// This is used by SyncController which is a hot path, this must be kept synchronous and functional.
    /// Any data required should be able to be passed in from the sync endpoint (and is probably already in scope).
    /// </summary>
    /// <param name="cipher">The item to be enriched.</param>
    /// <param name="allCollections">All collections the user is assigned to.</param>
    public static EnrichedCipherDetails EnrichCipher(CipherDetailsWithCollections cipher,
        IEnumerable<CollectionDetails> allCollections) =>
        new EnrichedCipherDetails(cipher, new ItemPermissions
        {
            CanDelete = ItemPermissionHelpers.CanDelete(cipher, allCollections)
        });
}
