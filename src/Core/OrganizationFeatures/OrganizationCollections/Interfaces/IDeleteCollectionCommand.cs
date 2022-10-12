using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;

public interface IDeleteCollectionCommand
{
    Task DeleteAsync(Collection collection);

    Task<ICollection<Collection>> DeleteManyAsync(Guid orgId, IEnumerable<Guid> collectionIds);
}
