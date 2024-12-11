using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;

public interface IBulkAddCollectionAccessCommand
{
    Task AddAccessAsync(
        ICollection<Collection> collections,
        ICollection<CollectionAccessSelection> users,
        ICollection<CollectionAccessSelection> groups
    );
}
