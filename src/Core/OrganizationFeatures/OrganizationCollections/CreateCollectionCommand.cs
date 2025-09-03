// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationCollections;

public class CreateCollectionCommand : ICreateCollectionCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICollectionRepository _collectionRepository;

    public CreateCollectionCommand(
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository)
    {
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _collectionRepository = collectionRepository;
    }

    public async Task<Collection> CreateAsync(Collection collection, IEnumerable<CollectionAccessSelection> groups = null,
        IEnumerable<CollectionAccessSelection> users = null)
    {
        if (collection.Type == CollectionType.DefaultUserCollection)
        {
            throw new BadRequestException("You cannot create a collection with the type as DefaultUserCollection.");
        }

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

        // Check max collections limit
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

        return collection;
    }
}
