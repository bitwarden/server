using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.Groups;

public class UpdateGroupCommand : IUpdateGroupCommand
{
    private readonly IEventService _eventService;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public UpdateGroupCommand(
        IEventService eventService,
        IGroupRepository groupRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task UpdateGroupAsync(Group group, Organization organization,
        IEnumerable<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> userIds = null)
    {
        Validate(organization);
        await GroupRepositoryUpdateGroupAsync(group, collections);

        if (userIds != null)
        {
            await GroupRepositoryUpdateUsersAsync(group, userIds);
        }

        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Updated);
    }

    public async Task UpdateGroupAsync(Group group, Organization organization, EventSystemUser systemUser,
        IEnumerable<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> userIds = null)
    {
        Validate(organization);
        await GroupRepositoryUpdateGroupAsync(group, collections);

        if (userIds != null)
        {
            await GroupRepositoryUpdateUsersAsync(group, userIds, systemUser);
        }

        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Updated, systemUser);
    }

    private async Task GroupRepositoryUpdateGroupAsync(Group group, IEnumerable<CollectionAccessSelection> collections = null)
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

    private async Task GroupRepositoryUpdateUsersAsync(Group group, IEnumerable<Guid> userIds, EventSystemUser? systemUser = null)
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

    private static void Validate(Organization organization)
    {
        if (organization == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!organization.UseGroups)
        {
            throw new BadRequestException("This organization cannot use groups.");
        }
    }
}
