using Bit.Core.Models.Data;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Authorization;

/// <summary>
/// Contains the lowest level of logic determining what a user can do with an item from their individual vault.
/// This is purely functional so that it can be reused in multiple locations without being tied to a specific source
/// of state.
/// </summary>
public static class ItemPermissionHelpers
{
    /// <summary>
    /// Whether a user can delete an item.
    /// </summary>
    /// <param name="userId">The User ID for the user whose permissions are being evaluated.</param>
    /// <param name="cipher">The item to be deleted.</param>
    /// <param name="allCollections">All collections the user is assigned to.</param>
    public static bool CanDelete(Guid userId, CipherDetailsWithCollections cipher, IEnumerable<CollectionDetails> allCollections) =>
        ItemIsOwnedByUser(userId, cipher) ||
        CanManageAtLeastOneCollection(cipher, allCollections);

    // TODO: CanEdit, ViewPasswords, maybe AssignToCollections

    // Private helper methods that can be reused between different actions
    private static bool CanManageAtLeastOneCollection(CipherDetailsWithCollections cipher, IEnumerable<CollectionDetails> allCollections) =>
        allCollections
            .Where(collection => cipher.CollectionIds.Contains(collection.Id))
            .Any(collection => collection.Manage);

    private static bool ItemIsOwnedByUser(Guid userId, Cipher cipher) =>
        cipher.UserId == userId;
}
