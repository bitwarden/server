using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;

public interface IUpdateCollectionCommand
{
    /// <summary>
    /// Updates a collection.
    /// </summary>
    /// <param name="collection">The collection to update.</param>
    /// <param name="groups">(Optional) The groups that will have access to the collection.</param>
    /// <param name="users">(Optional) The users that will have access to the collection.</param>
    /// <returns>The updated collection.</returns>
    Task<Collection> UpdateAsync(Collection collection, IEnumerable<CollectionAccessSelection> groups = null,
        IEnumerable<CollectionAccessSelection> users = null);
}
