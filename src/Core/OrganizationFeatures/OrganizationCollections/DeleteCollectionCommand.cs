using Bit.Core.Entities;
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
        await _collectionRepository.DeleteAsync(collection);
        await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Deleted);
    }
    public async Task<ICollection<Collection>> DeleteManyAsync(Guid orgId, IEnumerable<Guid> collectionIds)
    {
        var ids = collectionIds as Guid[] ?? collectionIds.ToArray();
        var collectionsToDelete = await _collectionRepository.GetManyByManyIdsAsync(ids);
        var filteredCollections = collectionsToDelete.Where(c => c.OrganizationId == orgId).ToList();
        if (!filteredCollections.Any())
        {
            throw new BadRequestException("No collections found.");
        }

        var deleteDate = DateTime.UtcNow;
        foreach (var collection in filteredCollections)
        {
            await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Deleted, deleteDate);
        }

        await _collectionRepository.DeleteManyAsync(orgId, filteredCollections.Select(g => g.Id));

        return filteredCollections;
    }
}
