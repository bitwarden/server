using Bit.Core.Entities;
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
        await _collectionRepository.DeleteAsync(collection);
        await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Deleted);
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> collectionIds)
    {
        var ids = collectionIds as Guid[] ?? collectionIds.ToArray();
        var collectionsToDelete = await _collectionRepository.GetManyByManyIdsAsync(ids);
        await this.DeleteManyAsync(collectionsToDelete);
    }

    public async Task DeleteManyAsync(IEnumerable<Collection> collections)
    {
        await _collectionRepository.DeleteManyAsync(collections.Select(c => c.Id));

        var deleteDate = DateTime.UtcNow;
        foreach (var collection in collections)
        {
            await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Deleted, deleteDate);
        }
    }
}
