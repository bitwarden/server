using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;

public interface IBulkAddCollectionAccessCommand
{
    Task AddAccessAsync(Guid organizationId, ICollection<Guid> collectionIds,
        ICollection<CollectionAccessSelection> users, ICollection<CollectionAccessSelection> groups);
}
