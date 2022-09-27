using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class CollectionService : ICollectionService
{
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailService _mailService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;

    public CollectionService(
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IUserRepository userRepository,
        IMailService mailService,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext)
    {
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
        _userRepository = userRepository;
        _mailService = mailService;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
    }

    public async Task SaveAsync(Collection collection, IEnumerable<SelectionReadOnly> groups = null,
        Guid? assignUserId = null)
    {
        var org = await _organizationRepository.GetByIdAsync(collection.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (collection.Id == default(Guid))
        {
            if (org.MaxCollections.HasValue)
            {
                var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(org.Id);
                if (org.MaxCollections.Value <= collectionCount)
                {
                    throw new BadRequestException("You have reached the maximum number of collections " +
                    $"({org.MaxCollections.Value}) for this organization.");
                }
            }

            if (groups == null || !org.UseGroups)
            {
                await _collectionRepository.CreateAsync(collection);
            }
            else
            {
                await _collectionRepository.CreateAsync(collection, groups);
            }

            // Assign a user to the newly created collection.
            if (assignUserId.HasValue)
            {
                var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, assignUserId.Value);
                if (orgUser != null && orgUser.Status == Enums.OrganizationUserStatusType.Confirmed)
                {
                    await _collectionRepository.UpdateUsersAsync(collection.Id,
                        new List<SelectionReadOnly> {
                            new SelectionReadOnly { Id = orgUser.Id, ReadOnly = false } });
                }
            }

            await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Created);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.CollectionCreated, org));
        }
        else
        {
            if (!org.UseGroups)
            {
                await _collectionRepository.ReplaceAsync(collection);
            }
            else
            {
                await _collectionRepository.ReplaceAsync(collection, groups ?? new List<SelectionReadOnly>());
            }

            await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Updated);
        }
    }

    public async Task DeleteAsync(Collection collection)
    {
        await _collectionRepository.DeleteAsync(collection);
        await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Deleted);
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

    public async Task<IEnumerable<Collection>> GetOrganizationCollections(Guid organizationId)
    {
        if (!await _currentContext.ViewAllCollections(organizationId) && !await _currentContext.ManageUsers(organizationId))
        {
            throw new NotFoundException();
        }

        IEnumerable<Collection> orgCollections;
        if (await _currentContext.OrganizationAdmin(organizationId) || await _currentContext.ViewAllCollections(organizationId))
        {
            // Admins, Owners, Providers and Custom (with collection management permissions) can access all items even if not assigned to them
            orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(organizationId);
        }
        else
        {
            var collections = await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value);
            orgCollections = collections.Where(c => c.OrganizationId == organizationId);
        }

        return orgCollections;
    }
}
