using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class UpdateOrganizationUserGroupsCommand : IUpdateOrganizationUserGroupsCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public UpdateOrganizationUserGroupsCommand(
        IEventService eventService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds)
    {
        await _organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupIds);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }
}
