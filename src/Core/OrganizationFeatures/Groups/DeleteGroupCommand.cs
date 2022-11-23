using Bit.Core.Entities;
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

    public async Task DeleteAsync(Group group)
    {
        await _groupRepository.DeleteAsync(group);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Deleted);
    }

    public async Task DeleteManyAsync(ICollection<Group> groups)
    {
        await _eventService.LogGroupEventsAsync(
            groups.Select(g =>
                (g, Enums.EventType.Group_Deleted, (DateTime?)DateTime.UtcNow)
            ));

        await _groupRepository.DeleteManyAsync(
            groups.Select(g => g.Id)
            );
    }
}
