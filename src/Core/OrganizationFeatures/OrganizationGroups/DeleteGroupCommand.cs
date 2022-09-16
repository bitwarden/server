using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using SendGrid.Helpers.Errors.Model;

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

    public async Task<ICollection<Group>> DeleteManyAsync(Guid orgId, IEnumerable<Guid> groupIds)
    {
        var ids = groupIds as Guid[] ?? groupIds.ToArray();
        var groupsToDelete = await _groupRepository.GetManyByManyIds(ids);
        var filteredGroups = groupsToDelete.Where(g => g.OrganizationId == orgId).ToList();

        if (!filteredGroups.Any())
        {
            throw new BadRequestException("Groups invalid.");
        }

        var deleteDate = DateTime.UtcNow;
        foreach (var group in filteredGroups)
        {
            await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Deleted, deleteDate);
        }

        await _groupRepository.DeleteManyAsync(orgId, filteredGroups.Select(g => g.Id));

        return filteredGroups;
    }
}
