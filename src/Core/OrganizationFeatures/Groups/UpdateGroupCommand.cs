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

    public UpdateGroupCommand(
        IEventService eventService,
        IGroupRepository groupRepository)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
    }

    public async Task UpdateGroupAsync(Group group, Organization organization,
        IEnumerable<SelectionReadOnly> collections = null)
    {
        Validate(organization);
        await GroupRepositoryUpdateGroupAsync(group, collections);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Updated);
    }

    public async Task UpdateGroupAsync(Group group, Organization organization, EventSystemUser systemUser,
        IEnumerable<SelectionReadOnly> collections = null)
    {
        Validate(organization);
        await GroupRepositoryUpdateGroupAsync(group, collections);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Updated, systemUser);
    }

    private async Task GroupRepositoryUpdateGroupAsync(Group group, IEnumerable<SelectionReadOnly> collections = null)
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
