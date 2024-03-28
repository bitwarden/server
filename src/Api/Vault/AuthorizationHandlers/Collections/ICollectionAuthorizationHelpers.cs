#nullable enable

using System.Security.Claims;
using Bit.Api.Models.Request;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public interface ICollectionAuthorizationHelpers
{
    /// <summary>
    /// Check that the current user is authorized to modify the given collectionAccess, and concat the collectionAccess
    /// with any collectionAccess that the current user is not authorized to modify.
    /// </summary>
    /// <remarks>
    /// When editing an orgUser or group, the client will only send updated collectionAccess that the saving
    /// user has permissions to modify. We need to match these up with CollectionAccessSelections
    /// that the saving user doesn't have permissions to modify, so that we don't blast them away when we update the database.
    /// </remarks>
    /// <param name="user">The current user</param>
    /// <param name="organizationAbility">The organization being updated</param>
    /// <param name="updatedCollectionAccess">The updated collection access sent by the client (being only those collections the current user can modify)</param>
    /// <returns>A complete list of all collection access to be written back to the database</returns>
    public Task<List<CollectionAccessSelection>> GetCollectionAccessToUpdateAsync(
        ClaimsPrincipal user,
        OrganizationAbility organizationAbility,
        IEnumerable<SelectionReadOnlyRequestModel>? updatedCollectionAccess);
}
