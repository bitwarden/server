using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;

public interface ICreateCollectionCommand
{
    /// <summary>
    /// Creates a new collection.
    /// </summary>
    /// <param name="collection">The collection to create.</param>
    /// <param name="groups">(Optional) The groups that will have access to the collection.</param>
    /// <param name="users">(Optional) The users that will have access to the collection.</param>
    /// <returns>The created collection.</returns>
    Task<Collection> CreateAsync(Collection collection, IEnumerable<CollectionAccessSelection> groups = null,
        IEnumerable<CollectionAccessSelection> users = null);
}
