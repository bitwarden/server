using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class GroupService : IGroupService
{
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;

    public GroupService(
        IEventService eventService,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository)
    {
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
    }

    [Obsolete("IDeleteGroupCommand should be used instead. To be removed by EC-608.")]
    public async Task DeleteAsync(Group group)
    {
        await _groupRepository.DeleteAsync(group);
        await _eventService.LogGroupEventAsync(group, EventType.Group_Deleted);
    }

    [Obsolete("IDeleteGroupCommand should be used instead. To be removed by EC-608.")]
    public async Task DeleteAsync(Group group, EventSystemUser systemUser)
    {
        await _groupRepository.DeleteAsync(group);
        await _eventService.LogGroupEventAsync(group, EventType.Group_Deleted, systemUser);
    }

    public async Task DeleteUserAsync(GroupUser groupUser)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(groupUser.OrganizationUserId);
        await _groupRepository.DeleteUserAsync(groupUser.GroupId, groupUser.OrganizationUserId);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_UpdatedGroups);
    }

    public async Task DeleteUserAsync(GroupUser groupUser, EventSystemUser systemUser)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(groupUser.OrganizationUserId);
        await _groupRepository.DeleteUserAsync(groupUser.GroupId, groupUser.OrganizationUserId);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_UpdatedGroups, systemUser);
    }
}
