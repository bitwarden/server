using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface ICollectionService
{
    Task SaveAsync(
        Collection collection,
        IEnumerable<CollectionAccessSelection> groups = null,
        IEnumerable<CollectionAccessSelection> users = null
    );
    Task DeleteUserAsync(Collection collection, Guid organizationUserId);

    [Obsolete("Pre-Flexible Collections logic.")]
    Task<IEnumerable<Collection>> GetOrganizationCollectionsAsync(Guid organizationId);
}
