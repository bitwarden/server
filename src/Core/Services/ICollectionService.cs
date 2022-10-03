using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface ICollectionService
{
    Task SaveAsync(Collection collection, IEnumerable<SelectionReadOnly> groups = null, Guid? assignUserId = null);
    Task DeleteAsync(Collection collection);
    Task DeleteUserAsync(Collection collection, Guid organizationUserId);
    Task<IEnumerable<Collection>> GetOrganizationCollections(Guid organizationId);
}
