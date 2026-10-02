using Bit.Core.AdminConsole.OrganizationFeatures.Collections.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections;

public class BulkAddCollectionAccessCommand : IBulkAddCollectionAccessCommand
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionAccessValidator _collectionAccessValidator;
    private readonly IEventService _eventService;
    private readonly TimeProvider _timeProvider;

    public BulkAddCollectionAccessCommand(
        ICollectionRepository collectionRepository,
        ICollectionAccessValidator collectionAccessValidator,
        IEventService eventService,
        TimeProvider timeProvider)
    {
        _collectionRepository = collectionRepository;
        _collectionAccessValidator = collectionAccessValidator;
        _eventService = eventService;
        _timeProvider = timeProvider;
    }

    public async Task AddAccessAsync(ICollection<Collection> collections,
        ICollection<CollectionAccessSelection> users,
        ICollection<CollectionAccessSelection> groups)
    {
        await ValidateRequestAsync(collections, users, groups);

        var revisionDate = _timeProvider.GetUtcNow().UtcDateTime;

        await _collectionRepository.CreateOrUpdateAccessForManyAsync(
            collections.First().OrganizationId,
            collections.Select(c => c.Id),
            users,
            groups,
            revisionDate
        );

        await _eventService.LogCollectionEventsAsync(collections.Select(c =>
            (c, EventType.Collection_Updated, (DateTime?)revisionDate)));
    }

    private async Task ValidateRequestAsync(ICollection<Collection> collections, ICollection<CollectionAccessSelection> usersAccess, ICollection<CollectionAccessSelection> groupsAccess)
    {
        if (collections == null || collections.Count == 0)
        {
            throw new BadRequestException("No collections were provided.");
        }

        if (collections.Any(c => c.Type == CollectionType.DefaultUserCollection))
        {
            throw new BadRequestException("You cannot add access to collections with the type as DefaultUserCollection.");
        }

        var orgId = collections.First().OrganizationId;

        if (collections.Any(c => c.OrganizationId != orgId))
        {
            throw new BadRequestException("All collections must belong to the same organization.");
        }

        var accessValidation = await _collectionAccessValidator.ValidateAsync(
            new CollectionAccessValidationRequest(orgId, groupsAccess?.ToList(), usersAccess?.ToList()));
        if (accessValidation.IsError)
        {
            throw new BadRequestException(accessValidation.AsError.Message);
        }
    }
}
