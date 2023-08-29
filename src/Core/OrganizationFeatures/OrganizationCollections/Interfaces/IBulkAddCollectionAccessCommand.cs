using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;

public interface IBulkAddCollectionAccessCommand
{
    Task AddAccessAsync(Guid organizationId, ICollection<ICollectionAccess> access);
}
