using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections;

public class DeleteCollectionCommand : IDeleteCollectionCommand
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IEventService _eventService;

    public DeleteCollectionCommand(
        ICollectionRepository collectionRepository,
        IEventService eventService)
    {
        _collectionRepository = collectionRepository;
        _eventService = eventService;
    }

    public async Task DeleteAsync(Collection collection)
    {
        if (collection.Type == CollectionType.DefaultUserCollection)
        {
            throw new BadRequestException("You cannot delete a collection with the type as DefaultUserCollection.");
        }

        await _collectionRepository.DeleteAsync(collection);
        await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Deleted, DateTime.UtcNow);
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> collectionIds)
    {
        var ids = collectionIds as Guid[] ?? collectionIds.ToArray();
        var collectionsToDelete = await _collectionRepository.GetManyByManyIdsAsync(ids);
        await this.DeleteManyAsync(collectionsToDelete);
    }

    public async Task DeleteManyAsync(IEnumerable<Collection> collections)
    {
        if (collections.Any(c => c.Type == Enums.CollectionType.DefaultUserCollection))
        {
            throw new BadRequestException("You cannot delete collections with the type as DefaultUserCollection.");
        }

        await _collectionRepository.DeleteManyAsync(collections.Select(c => c.Id));
        await _eventService.LogCollectionEventsAsync(collections.Select(c => (c, Enums.EventType.Collection_Deleted, (DateTime?)DateTime.UtcNow)));
    }
}
