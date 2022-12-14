using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.Groups;

public class CreateGroupCommand : ICreateGroupCommand
{
    private readonly IEventService _eventService;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IReferenceEventService _referenceEventService;

    public CreateGroupCommand(
        IEventService eventService,
        IGroupRepository groupRepository,
        IOrganizationUserRepository organizationUserRepository,
        IReferenceEventService referenceEventService)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
        _organizationUserRepository = organizationUserRepository;
        _referenceEventService = referenceEventService;
    }

    public async Task CreateGroupAsync(Group group, Organization organization,
        IEnumerable<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> users = null)
    {
        Validate(organization);
        await GroupRepositoryCreateGroupAsync(group, organization, collections);
        
        if (users != null)
        {
            await GroupRepositoryUpdateUsersAsync(group, users);
        }
        
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Created);
    }

    public async Task CreateGroupAsync(Group group, Organization organization, EventSystemUser systemUser,
        IEnumerable<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> users = null)
    {
        Validate(organization);
        await GroupRepositoryCreateGroupAsync(group, organization, collections);

        if (users != null)
        {
            await GroupRepositoryUpdateUsersAsync(group, users, systemUser);
        }
        
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Created, systemUser);
    }

    private async Task GroupRepositoryCreateGroupAsync(Group group, Organization organization, IEnumerable<CollectionAccessSelection> collections = null)
    {
        group.CreationDate = group.RevisionDate = DateTime.UtcNow;

        if (collections == null)
        {
            await _groupRepository.CreateAsync(group);
        }
        else
        {
            await _groupRepository.CreateAsync(group, collections);
        }

        await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.GroupCreated, organization));
    }

    private async Task GroupRepositoryUpdateUsersAsync(Group group, IEnumerable<Guid> userIds,
        EventSystemUser? systemUser = null)
    {
        var usersToAddToGroup = userIds as Guid[] ?? userIds.ToArray();

        await _groupRepository.UpdateUsersAsync(group.Id, usersToAddToGroup);

        var users = await _organizationUserRepository.GetManyAsync(usersToAddToGroup);
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
