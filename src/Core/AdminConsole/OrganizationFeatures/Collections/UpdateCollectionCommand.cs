// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.OrganizationFeatures.Collections.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections;

public class UpdateCollectionCommand : IUpdateCollectionCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly TimeProvider _timeProvider;

    public UpdateCollectionCommand(
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        TimeProvider timeProvider)
    {
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _collectionRepository = collectionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Collection> UpdateAsync(Collection collection, IEnumerable<CollectionAccessSelection> groups = null,
        IEnumerable<CollectionAccessSelection> users = null)
    {
        if (collection.Type == CollectionType.DefaultUserCollection)
        {
            throw new BadRequestException("You cannot edit a collection with the type as DefaultUserCollection.");
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

        // A collection should always have someone with Can Manage permissions.
        // When groups or users is null it means "don't change that association", so we must
        // fall back to the existing relationships when evaluating this rule.
        if (!org.AllowAdminAccessToAllCollectionItems)
        {
            IEnumerable<CollectionAccessSelection> groupsForValidation = groupsList;
            IEnumerable<CollectionAccessSelection> usersForValidation = usersList;

            if (groupsForValidation == null || usersForValidation == null)
            {
                var (_, currentAccess) = await _collectionRepository.GetByIdWithAccessAsync(collection.Id);
                groupsForValidation ??= currentAccess.Groups;
                usersForValidation ??= currentAccess.Users;
            }

            var groupHasManageAccess = groupsForValidation?.Any(g => g.Manage) ?? false;
            var userHasManageAccess = usersForValidation?.Any(u => u.Manage) ?? false;
            if (!groupHasManageAccess && !userHasManageAccess)
            {
                throw new BadRequestException(
                    "At least one member or group must have can manage permission.");
            }
        }

        collection.RevisionDate = _timeProvider.GetUtcNow().UtcDateTime;
        await _collectionRepository.ReplaceAsync(collection, org.UseGroups ? groupsList : null, usersList);
        await _eventService.LogCollectionEventAsync(collection, EventType.Collection_Updated);

        return collection;
    }
}
