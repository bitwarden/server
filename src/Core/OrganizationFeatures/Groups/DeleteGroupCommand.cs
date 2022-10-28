using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.Groups;

public class DeleteGroupCommand : IDeleteGroupCommand
{
    private readonly IEventService _eventService;
    private readonly IGroupRepository _groupRepository;

    public DeleteGroupCommand(IEventService eventService, IGroupRepository groupRepository)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
    }

    public async Task DeleteGroupAsync(Guid organizationId, Guid id)
    {
        var group = await GroupRepositoryDeleteGroupAsync(organizationId, id);
        await _eventService.LogGroupEventAsync(group, Core.Enums.EventType.Group_Deleted);
    }

    public async Task DeleteGroupAsync(Guid organizationId, Guid id, EventSystemUser eventSystemUser)
    {
        var group = await GroupRepositoryDeleteGroupAsync(organizationId, id);
        await _eventService.LogGroupEventAsync(group, Core.Enums.EventType.Group_Deleted, eventSystemUser);
    }

    private async Task<Group> GroupRepositoryDeleteGroupAsync(Guid organizationId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organizationId)
        {
            throw new NotFoundException("Group not found.");
        }

        await _groupRepository.DeleteAsync(group);

        return group;
    }
}
