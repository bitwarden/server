using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups.Interfaces;

namespace Bit.Scim.Commands.Groups;

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
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organizationId)
        {
            throw new NotFoundException("Group not found.");
        }

        await _groupRepository.DeleteAsync(group);
        await _eventService.LogGroupEventAsync(group, Core.Enums.EventType.Group_Deleted);
    }
}
