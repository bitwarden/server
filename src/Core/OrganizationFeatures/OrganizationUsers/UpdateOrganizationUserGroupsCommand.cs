using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class UpdateOrganizationUserGroupsCommand : OrganizationUserCommand, IUpdateOrganizationUserGroupsCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public UpdateOrganizationUserGroupsCommand(ICurrentContext currentContext, IEventService eventService, IOrganizationRepository organizationRepository, IOrganizationUserRepository organizationUserRepository) : base(currentContext, organizationRepository)
    {
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds, Guid? loggedInUserId)
    {
        if (loggedInUserId.HasValue)
        {
            await ValidateOrganizationUserUpdatePermissions(organizationUser.OrganizationId, organizationUser.Type, null, organizationUser.GetPermissions());
        }
        await _organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupIds);
        await _eventService.LogOrganizationUserEventAsync(organizationUser,
            EventType.OrganizationUser_UpdatedGroups);
    }
}
