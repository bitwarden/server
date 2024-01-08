using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class UpdateOrganizationUserGroupsCommand : IUpdateOrganizationUserGroupsCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;

    public UpdateOrganizationUserGroupsCommand(
        IEventService eventService,
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository,
        IApplicationCacheService applicationCacheService)
    {
        _eventService = eventService;
        _organizationService = organizationService;
        _organizationUserRepository = organizationUserRepository;
        _applicationCacheService = applicationCacheService;
    }

    public async Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds, Guid? loggedInUserId)
    {
        if (loggedInUserId.HasValue)
        {
            var organizationAbility =
                await _applicationCacheService.GetOrganizationAbilityAsync(organizationUser.OrganizationId);
            await _organizationService.ValidateOrganizationUserUpdatePermissions(organizationAbility, organizationUser.Type, null, organizationUser.GetPermissions());
        }
        await _organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupIds);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }
}
