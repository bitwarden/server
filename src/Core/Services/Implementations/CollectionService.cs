#nullable enable

using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class CollectionService : ICollectionService
{
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;

    public CollectionService(
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
    }

    public async Task SaveAsync(Collection collection, IEnumerable<CollectionAccessSelection>? groups = null,
        IEnumerable<CollectionAccessSelection>? users = null)
    {
        var org = await _organizationRepository.GetByIdAsync(collection.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        var groupsList = groups?.ToList();
        var usersList = users?.ToList();

        // Cannot use Manage with ReadOnly/HidePasswords permissions
        var invalidAssociations = groupsList?.Where(cas => cas.Manage && (cas.ReadOnly || cas.HidePasswords));
        if (invalidAssociations?.Any() ?? false)
        {
            throw new BadRequestException("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
        }

        // A collection should always have someone with Can Manage permissions
        var groupHasManageAccess = groupsList?.Any(g => g.Manage) ?? false;
        var userHasManageAccess = usersList?.Any(u => u.Manage) ?? false;
        if (!groupHasManageAccess && !userHasManageAccess && !org.AllowAdminAccessToAllCollectionItems)
        {
            throw new BadRequestException(
                "At least one member or group must have can manage permission.");
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

            await _collectionRepository.CreateAsync(collection, org.UseGroups ? groupsList : null, usersList);
            await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Created);
        }
        else
        {
            await _collectionRepository.ReplaceAsync(collection, org.UseGroups ? groupsList : null, usersList);
            await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Updated);
        }
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
