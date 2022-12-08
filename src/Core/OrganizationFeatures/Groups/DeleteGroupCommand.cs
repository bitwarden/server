using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.Groups;

public class DeleteGroupCommand : IDeleteGroupCommand
{
    private readonly IGroupRepository _groupRepository;
    private readonly IEventService _eventService;

    public DeleteGroupCommand(IGroupRepository groupRepository, IEventService eventService)
    {
        _groupRepository = groupRepository;
        _eventService = eventService;
    }
    
    public async Task DeleteGroupAsync(Guid organizationId, Guid id)
    {
        var group = await GroupRepositoryDeleteGroupAsync(organizationId, id);
        await _eventService.LogGroupEventAsync(group, EventType.Group_Deleted);
    }

    public async Task DeleteGroupAsync(Guid organizationId, Guid id, EventSystemUser eventSystemUser)
    {
        var group = await GroupRepositoryDeleteGroupAsync(organizationId, id);
        await _eventService.LogGroupEventAsync(group, EventType.Group_Deleted, eventSystemUser);
    }

    public async Task DeleteAsync(Group group)
    {
        await _groupRepository.DeleteAsync(group);
        await _eventService.LogGroupEventAsync(group, EventType.Group_Deleted);
    }

    public async Task DeleteManyAsync(ICollection<Group> groups)
    {
        await _eventService.LogGroupEventsAsync(
            groups.Select(g =>
                (g, EventType.Group_Deleted, (EventSystemUser?)null, (DateTime?)DateTime.UtcNow)
            ));

        await _groupRepository.DeleteManyAsync(
            groups.Select(g => g.Id)
            );
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
