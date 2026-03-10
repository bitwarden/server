using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections;

public class DeleteCollectionCommand : IDeleteCollectionCommand
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IEventService _eventService;
    private readonly ILogger<DeleteCollectionCommand> _logger;

    public DeleteCollectionCommand(
        ICollectionRepository collectionRepository,
        IEventService eventService,
        ILogger<DeleteCollectionCommand> logger)
    {
        _collectionRepository = collectionRepository;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task DeleteAsync(Collection collection)
    {
        if (collection.Type == CollectionType.DefaultUserCollection)
        {
            throw new BadRequestException("You cannot delete a collection with the type as DefaultUserCollection.");
        }

        await _collectionRepository.DeleteAsync(collection);

        try
        {
            await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Deleted, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log collection deletion event for collection {CollectionId}", collection.Id);
        }
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

        try
        {
            await _eventService.LogCollectionEventsAsync(collections.Select(c => (c, Enums.EventType.Collection_Deleted, (DateTime?)DateTime.UtcNow)));
        }
        catch (Exception ex)
        {
            var collectionIds = string.Join(", ", collections.Select(c => c.Id));
            _logger.LogError(ex, "Failed to log collection deletion events for collections: {CollectionIds}", collectionIds);
        }
    }
}
