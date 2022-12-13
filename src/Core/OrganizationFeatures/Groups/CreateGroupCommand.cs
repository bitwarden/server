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
    private readonly IReferenceEventService _referenceEventService;

    public CreateGroupCommand(
        IEventService eventService,
        IGroupRepository groupRepository,
        IReferenceEventService referenceEventService)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
        _referenceEventService = referenceEventService;
    }

    public async Task CreateGroupAsync(Group group, Organization organization,
        IEnumerable<SelectionReadOnly> collections = null)
    {
        Validate(organization);
        await GroupRepositoryCreateGroupAsync(group, organization, collections);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Created);
    }

    public async Task CreateGroupAsync(Group group, Organization organization, EventSystemUser systemUser,
        IEnumerable<SelectionReadOnly> collections = null)
    {
        Validate(organization);
        await GroupRepositoryCreateGroupAsync(group, organization, collections);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Created, systemUser);
    }

    private async Task GroupRepositoryCreateGroupAsync(Group group, Organization organization, IEnumerable<SelectionReadOnly> collections = null)
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
