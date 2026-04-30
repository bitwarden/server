using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.Interfaces;

public interface IDeleteCollectionCommand
{
    Task DeleteAsync(Collection collection);
    Task DeleteManyAsync(IEnumerable<Guid> collectionIds);
    Task DeleteManyAsync(IEnumerable<Collection> collections);
}
