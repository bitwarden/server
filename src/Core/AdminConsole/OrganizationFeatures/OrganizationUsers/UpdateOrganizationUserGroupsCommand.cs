using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class UpdateOrganizationUserGroupsCommand : IUpdateOrganizationUserGroupsCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IScopedOrganizationApiKeyContext _scopedOrganizationApiKeyContext;

    public UpdateOrganizationUserGroupsCommand(
        IEventService eventService,
        IOrganizationUserRepository organizationUserRepository,
        TimeProvider timeProvider,
        IScopedOrganizationApiKeyContext scopedOrganizationApiKeyContext)
    {
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
        _timeProvider = timeProvider;
        _scopedOrganizationApiKeyContext = scopedOrganizationApiKeyContext;
    }

    public async Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds)
    {
        if (_scopedOrganizationApiKeyContext.IsScopedOrganizationApiKey && organizationUser.Type != OrganizationUserType.User)
        {
            throw new BadRequestException(new ScopedApiKeyCanOnlyManageUsers().Message);
        }

        await _organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupIds, _timeProvider.GetUtcNow().UtcDateTime);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }
}
