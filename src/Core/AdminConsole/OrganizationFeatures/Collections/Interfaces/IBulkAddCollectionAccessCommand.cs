using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.Interfaces;

public interface IBulkAddCollectionAccessCommand
{
    Task AddAccessAsync(ICollection<Collection> collections,
        ICollection<CollectionAccessSelection> users, ICollection<CollectionAccessSelection> groups);
}
