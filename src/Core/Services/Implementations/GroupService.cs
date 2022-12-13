using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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

    public async Task DeleteUserAsync(Group group, Guid organizationUserId)
    {
        var orgUser = await GroupRepositoryDeleteUserAsync(group, organizationUserId, systemUser: null);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_UpdatedGroups);
    }

    public async Task DeleteUserAsync(Group group, Guid organizationUserId, EventSystemUser systemUser)
    {
        var orgUser = await GroupRepositoryDeleteUserAsync(group, organizationUserId, systemUser);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_UpdatedGroups, systemUser);
    }

    private async Task<OrganizationUser> GroupRepositoryDeleteUserAsync(Group group, Guid organizationUserId, EventSystemUser? systemUser)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != group.OrganizationId)
        {
            throw new NotFoundException();
        }

        await _groupRepository.DeleteUserAsync(group.Id, organizationUserId);

        return orgUser;
    }
}
