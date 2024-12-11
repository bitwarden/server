using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections;

public class BulkAddCollectionAccessCommand : IBulkAddCollectionAccessCommand
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IEventService _eventService;

    public BulkAddCollectionAccessCommand(
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        IEventService eventService
    )
    {
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
        _eventService = eventService;
    }

    public async Task AddAccessAsync(
        ICollection<Collection> collections,
        ICollection<CollectionAccessSelection> users,
        ICollection<CollectionAccessSelection> groups
    )
    {
        await ValidateRequestAsync(collections, users, groups);

        await _collectionRepository.CreateOrUpdateAccessForManyAsync(
            collections.First().OrganizationId,
            collections.Select(c => c.Id),
            users,
            groups
        );

        await _eventService.LogCollectionEventsAsync(
            collections.Select(c => (c, EventType.Collection_Updated, (DateTime?)DateTime.UtcNow))
        );
    }

    private async Task ValidateRequestAsync(
        ICollection<Collection> collections,
        ICollection<CollectionAccessSelection> usersAccess,
        ICollection<CollectionAccessSelection> groupsAccess
    )
    {
        if (collections == null || collections.Count == 0)
        {
            throw new BadRequestException("No collections were provided.");
        }

        var orgId = collections.First().OrganizationId;

        if (collections.Any(c => c.OrganizationId != orgId))
        {
            throw new BadRequestException("All collections must belong to the same organization.");
        }

        var collectionUserIds = usersAccess?.Select(u => u.Id).Distinct().ToList();

        if (collectionUserIds is { Count: > 0 })
        {
            var users = await _organizationUserRepository.GetManyAsync(collectionUserIds);

            if (users.Count != collectionUserIds.Count)
            {
                throw new BadRequestException("One or more users do not exist.");
            }

            if (users.Any(u => u.OrganizationId != orgId))
            {
                throw new BadRequestException(
                    "One or more users do not belong to the same organization as the collection being assigned."
                );
            }
        }

        var collectionGroupIds = groupsAccess?.Select(g => g.Id).Distinct().ToList();

        if (collectionGroupIds is { Count: > 0 })
        {
            var groups = await _groupRepository.GetManyByManyIds(collectionGroupIds);

            if (groups.Count != collectionGroupIds.Count)
            {
                throw new BadRequestException("One or more groups do not exist.");
            }

            if (groups.Any(g => g.OrganizationId != orgId))
            {
                throw new BadRequestException(
                    "One or more groups do not belong to the same organization as the collection being assigned."
                );
            }
        }
    }
}
