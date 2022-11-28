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
    private readonly IOrganizationRepository _organizationRepository;

    public UpdateGroupCommand(
        IEventService eventService,
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
        _organizationRepository = organizationRepository;
    }

    public async Task UpdateGroupAsync(Group group,
        IEnumerable<SelectionReadOnly> collections = null)
    {
        await GroupRepositoryUpdateGroupAsync(group, collections);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Updated);
    }

    public async Task UpdateGroupAsync(Group group, EventSystemUser systemUser,
        IEnumerable<SelectionReadOnly> collections = null)
    {
        await GroupRepositoryUpdateGroupAsync(group, collections);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Updated, systemUser);
    }

    private async Task GroupRepositoryUpdateGroupAsync(Group group, IEnumerable<SelectionReadOnly> collections = null)
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
}
