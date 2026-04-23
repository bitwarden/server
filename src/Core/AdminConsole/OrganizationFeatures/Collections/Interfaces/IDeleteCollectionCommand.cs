using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;

public interface IDeleteCollectionCommand
{
    Task DeleteAsync(Collection collection);
    Task DeleteManyAsync(IEnumerable<Guid> collectionIds);
    Task DeleteManyAsync(IEnumerable<Collection> collections);
}
