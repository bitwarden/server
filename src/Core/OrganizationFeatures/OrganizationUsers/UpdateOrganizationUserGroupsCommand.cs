using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class UpdateOrganizationUserGroupsCommand : IUpdateOrganizationUserGroupsCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public UpdateOrganizationUserGroupsCommand(
        IEventService eventService,
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _eventService = eventService;
        _organizationService = organizationService;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds, Guid? loggedInUserId)
    {
        if (loggedInUserId.HasValue)
        {
            await _organizationService.ValidateOrganizationUserUpdatePermissions(organizationUser.OrganizationId, organizationUser.Type, null, organizationUser.GetPermissions());
        }
        await _organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupIds);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }
}
