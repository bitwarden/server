using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public class GroupCollectionAccessValidator(ICollectionRepository collectionRepository)
    : IGroupCollectionAccessValidator
{
    public async Task ValidateAsync(Guid organizationId, ICollection<CollectionAccessSelection> collectionAccess)
    {
        var collections = await collectionRepository
            .GetManyByManyIdsAsync(collectionAccess.Select(c => c.Id));
        var collectionIds = collections.Select(c => c.Id);

        var missingCollection = collectionAccess
            .FirstOrDefault(cas => !collectionIds.Contains(cas.Id));
        if (missingCollection != default)
        {
            throw new NotFoundException();
        }

        var invalidCollection = collections.FirstOrDefault(c => c.OrganizationId != organizationId);
        if (invalidCollection != default)
        {
            // Use generic error message to avoid enumeration
            throw new NotFoundException();
        }

        if (collections.Any(c => c.Type == CollectionType.DefaultUserCollection))
        {
            throw new BadRequestException("You cannot modify group access for collections with the type as DefaultUserCollection.");
        }
    }
}
