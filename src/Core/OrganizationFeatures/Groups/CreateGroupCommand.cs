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
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IReferenceEventService _referenceEventService;

    public CreateGroupCommand(
        IEventService eventService,
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        IReferenceEventService referenceEventService)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
        _organizationRepository = organizationRepository;
        _referenceEventService = referenceEventService;
    }

    public async Task CreateGroupAsync(Group group,
        IEnumerable<SelectionReadOnly> collections = null)
    {
        await GroupRepositoryCreateGroupAsync(group, collections);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Created);
    }

    public async Task CreateGroupAsync(Group group, EventSystemUser systemUser,
        IEnumerable<SelectionReadOnly> collections = null)
    {
        await GroupRepositoryCreateGroupAsync(group, collections);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Created, systemUser);
    }

    private async Task GroupRepositoryCreateGroupAsync(Group group, IEnumerable<SelectionReadOnly> collections = null)
    {
        var organization = await _organizationRepository.GetByIdAsync(group.OrganizationId);
        if (organization == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!organization.UseGroups)
        {
            throw new BadRequestException("This organization cannot use groups.");
        }

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
}
