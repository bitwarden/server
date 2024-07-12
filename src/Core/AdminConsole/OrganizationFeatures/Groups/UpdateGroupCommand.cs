#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public class UpdateGroupCommand : IUpdateGroupCommand
{
    private readonly IEventService _eventService;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;

    public UpdateGroupCommand(
        IEventService eventService,
        IGroupRepository groupRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
    }

    public async Task UpdateGroupAsync(Group group, Organization organization,
        ICollection<CollectionAccessSelection>? collections = null,
        IEnumerable<Guid>? userIds = null)
    {
        await ValidateAsync(organization, group, collections, userIds);

        await SaveGroupWithCollectionsAsync(group, collections);

        if (userIds != null)
        {
            await SaveGroupUsersAsync(group, userIds);
        }

        await _eventService.LogGroupEventAsync(group, Core.Enums.EventType.Group_Updated);
    }

    public async Task UpdateGroupAsync(Group group, Organization organization, EventSystemUser systemUser,
        ICollection<CollectionAccessSelection>? collections = null,
        IEnumerable<Guid>? userIds = null)
    {
        await ValidateAsync(organization, group, collections, userIds);

        await SaveGroupWithCollectionsAsync(group, collections);

        if (userIds != null)
        {
            await SaveGroupUsersAsync(group, userIds, systemUser);
        }

        await _eventService.LogGroupEventAsync(group, Core.Enums.EventType.Group_Updated, systemUser);
    }

    private async Task SaveGroupWithCollectionsAsync(Group group, IEnumerable<CollectionAccessSelection>? collections = null)
    {
        group.RevisionDate = DateTime.UtcNow;

        if (collections == null)
        {
            await _groupRepository.ReplaceAsync(group);
        }
        else
        {
            await _groupRepository.ReplaceAsync(group, collections);
        }
    }

    private async Task SaveGroupUsersAsync(Group group, IEnumerable<Guid> userIds, EventSystemUser? systemUser = null)
    {
        var newUserIds = userIds as Guid[] ?? userIds.ToArray();
        var originalUserIds = await _groupRepository.GetManyUserIdsByIdAsync(group.Id);

        await _groupRepository.UpdateUsersAsync(group.Id, newUserIds);

        // We only want to create events OrganizationUserEvents for those that were actually modified.
        // HashSet.SymmetricExceptWith is a convenient method of finding the difference between lists
        var changedUserIds = new HashSet<Guid>(originalUserIds);
        changedUserIds.SymmetricExceptWith(newUserIds);

        // Fetch all changed users for logging the event
        var users = await _organizationUserRepository.GetManyAsync(changedUserIds);
        var eventDate = DateTime.UtcNow;

        if (systemUser.HasValue)
        {
            await _eventService.LogOrganizationUserEventsAsync(users.Select(u =>
                (u, EventType.OrganizationUser_UpdatedGroups, systemUser.Value, (DateTime?)eventDate)));
        }
        else
        {
            await _eventService.LogOrganizationUserEventsAsync(users.Select(u =>
                (u, EventType.OrganizationUser_UpdatedGroups, (DateTime?)eventDate)));
        }
    }

    private async Task ValidateAsync(Organization organization, Group group, ICollection<CollectionAccessSelection>? collectionAccess,
        IEnumerable<Guid>? memberAccess)
    {
        // Avoid multiple enumeration
        memberAccess = memberAccess?.ToList();

        if (organization == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!organization.UseGroups)
        {
            throw new BadRequestException("This organization cannot use groups.");
        }

        var originalGroup = await _groupRepository.GetByIdAsync(group.Id);
        if (originalGroup == null)
        {
            throw new NotFoundException("Group not found.");
        }

        if (originalGroup.OrganizationId != group.OrganizationId)
        {
            throw new BadRequestException("You cannot change a group's organization id.");
        }

        if (collectionAccess?.Any() == true)
        {
            var collections = await _collectionRepository
                .GetManyByManyIdsAsync(collectionAccess.Select(c => c.Id));
            if (collections.Count != collectionAccess.Count() ||
                collections.Any(c => c.OrganizationId != originalGroup.OrganizationId))
            {
                throw new BadRequestException(
                    "A collection does not exist or you do not have permission to grant access to it.");
            }
        }

        if (memberAccess?.Any() == true)
        {
            var organizationUsers = await _organizationUserRepository.GetManyAsync(memberAccess);
            if (organizationUsers.Count != memberAccess.Count() ||
                organizationUsers.Any(ou => ou.OrganizationId != originalGroup.OrganizationId))
            {
                throw new BadRequestException(
                    "A member does not exist or you do not have permission to modify their group access.");
            }
        }

        if (organization.FlexibleCollections)
        {
            if (group.AccessAll)
            {
                throw new BadRequestException("The AccessAll property has been deprecated by collection enhancements. Assign the group to collections instead.");
            }

            var invalidAssociations = collectionAccess?.Where(cas => cas.Manage && (cas.ReadOnly || cas.HidePasswords));
            if (invalidAssociations?.Any() ?? false)
            {
                throw new BadRequestException("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
            }
        }
    }
}
