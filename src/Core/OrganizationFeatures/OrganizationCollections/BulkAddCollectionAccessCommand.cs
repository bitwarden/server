using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections;

public class BulkAddCollectionAccessCommand : IBulkAddCollectionAccessCommand
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;

    public BulkAddCollectionAccessCommand(
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository)
    {
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
    }

    public async Task AddAccessAsync(Guid organizationId, ICollection<Guid> collectionIds,
        ICollection<CollectionAccessSelection> users,
        ICollection<CollectionAccessSelection> groups)
    {
        await ValidateRequestAsync(organizationId, collectionIds, users, groups);

        // TODO: Add repository call and Event logs
    }

    private async Task ValidateRequestAsync(Guid orgId, ICollection<Guid> collectionIds, ICollection<CollectionAccessSelection> usersAccess, ICollection<CollectionAccessSelection> groupsAccess)
    {
        if (collectionIds.Count == 0)
        {
            throw new BadRequestException("No collections were provided.");
        }

        var collections = await _collectionRepository.GetManyByManyIdsAsync(collectionIds);

        if (collections.Count != collectionIds.Count)
        {
            throw new BadRequestException("One or more collections do not exist.");
        }

        if (collections.Any(c => c.OrganizationId != orgId))
        {
            throw new BadRequestException("All collections must belong to the same organization.");
        }

        var collectionUserIds = usersAccess.Select(u => u.Id).ToList();

        if (collectionUserIds.Count > 0)
        {
            var users = await _organizationUserRepository.GetManyAsync(collectionUserIds);

            if (users.Count != collectionUserIds.Count)
            {
                throw new BadRequestException("One or more users do not exist.");
            }

            if (users.Any(u => u.OrganizationId != orgId))
            {
                throw new BadRequestException("One or more users do not belong to the same organization as the collection being assigned.");
            }
        }

        var collectionGroupIds = groupsAccess.Select(g => g.Id).ToList();

        if (collectionGroupIds.Count > 0)
        {
            var groups = await _groupRepository.GetManyByManyIds(collectionGroupIds);

            if (groups.Count != collectionGroupIds.Count)
            {
                throw new BadRequestException("One or more groups do not exist.");
            }

            if (groups.Any(g => g.OrganizationId != orgId))
            {
                throw new BadRequestException("One or more groups do not belong to the same organization as the collection being assigned.");
            }
        }
    }
}
