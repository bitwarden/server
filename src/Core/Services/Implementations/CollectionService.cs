#nullable enable

using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class CollectionService : ICollectionService
{
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;

    public CollectionService(
        IEventService eventService,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
    }



    public async Task DeleteUserAsync(Collection collection, Guid organizationUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != collection.OrganizationId)
        {
            throw new NotFoundException();
        }
        await _collectionRepository.DeleteUserAsync(collection.Id, organizationUserId);
        await _eventService.LogOrganizationUserEventAsync(orgUser, Enums.EventType.OrganizationUser_Updated);
    }
}
