using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationGroups;

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
        
        var deleteDate = DateTime.UtcNow;
        foreach (var group in groupsToDelete)
        {
            await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Deleted, deleteDate);
        }

        await _groupRepository.DeleteManyAsync(groupsToDelete.Select(g => g.Id));
    }
}
