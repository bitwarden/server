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

    public async Task DeleteManyAsync(IEnumerable<Group> groups)
    {
        var groupsToDelete = groups as Group[] ?? groups.ToArray();

        await _eventService.LogGroupEventsAsync(
            groupsToDelete.Select(g =>
                (g, Enums.EventType.Group_Deleted, (DateTime?)DateTime.UtcNow)
            ));

        await _groupRepository.DeleteManyAsync(
            groupsToDelete.Select(g => g.Id)
            );
    }
}
