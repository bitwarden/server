using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public class GroupCollectionAccessValidator(ICollectionRepository collectionRepository)
    : IGroupCollectionAccessValidator
{
    public async Task<Error?> ValidateAsync(Guid organizationId, ICollection<CollectionAccessSelection> collectionAccess)
    {
        var collections = await collectionRepository
            .GetManyByManyIdsAsync(collectionAccess.Select(c => c.Id));

        // Collections in another organization are treated as not found, to avoid enumeration
        var organizationCollectionIds = collections
            .Where(c => c.OrganizationId == organizationId)
            .Select(c => c.Id)
            .ToHashSet();

        if (collectionAccess.Any(cas => !organizationCollectionIds.Contains(cas.Id)))
        {
            return new CollectionNotFound();
        }

        if (collections.Any(c => c.Type == CollectionType.DefaultUserCollection))
        {
            return new CannotModifyDefaultUserCollection();
        }

        return null;
    }
}
