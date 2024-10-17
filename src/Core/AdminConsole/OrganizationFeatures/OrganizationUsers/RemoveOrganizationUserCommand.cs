using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class RemoveOrganizationUserCommand : IRemoveOrganizationUserCommand
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly ICurrentContext _currentContext;
    private readonly IHasConfirmedOwnersExceptQuery _hasConfirmedOwnersExceptQuery;

    public RemoveOrganizationUserCommand(
        IDeviceRepository deviceRepository,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService,
        IPushNotificationService pushNotificationService,
        IPushRegistrationService pushRegistrationService,
        ICurrentContext currentContext,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery)
    {
        _deviceRepository = deviceRepository;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _currentContext = currentContext;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        ValidateDeleteUser(organizationId, organizationUser);

        await RepositoryDeleteUserAsync(organizationUser, deletingUserId);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        ValidateDeleteUser(organizationId, organizationUser);

        await RepositoryDeleteUserAsync(organizationUser, null);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed, eventSystemUser);
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        ValidateDeleteUser(organizationId, organizationUser);

        await RepositoryDeleteUserAsync(organizationUser, null);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    public async Task<List<Tuple<OrganizationUser, string>>> RemoveUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUsersId,
        Guid? deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUsersId);
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId)
            .ToList();

        if (!filteredUsers.Any())
        {
            throw new BadRequestException("Users invalid.");
        }

        if (!await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        var deletingUserIsOwner = false;
        if (deletingUserId.HasValue)
        {
            deletingUserIsOwner = await _currentContext.OrganizationOwner(organizationId);
        }

        var result = new List<Tuple<OrganizationUser, string>>();
        var usersToDelete = new List<OrganizationUser>();

        foreach (var orgUser in filteredUsers)
        {
            try
            {
                if (deletingUserId.HasValue && orgUser.UserId == deletingUserId)
                {
                    throw new BadRequestException("You cannot remove yourself.");
                }

                if (orgUser.Type == OrganizationUserType.Owner && deletingUserId.HasValue && !deletingUserIsOwner)
                {
                    throw new BadRequestException("Only owners can delete other owners.");
                }

                usersToDelete.Add(orgUser);
                result.Add(Tuple.Create(orgUser, ""));
            }
            catch (BadRequestException e)
            {
                result.Add(Tuple.Create(orgUser, e.Message));
            }
        }

        if (usersToDelete.Any())
        {
            await _organizationUserRepository.DeleteManyAsync(usersToDelete.Select(u => u.Id));

            foreach (var orgUser in usersToDelete)
            {
                await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Removed);

                if (orgUser.UserId.HasValue)
                {
                    await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
                }
            }
        }

        return result;
    }

    public async Task UserLeaveAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        ValidateDeleteUser(organizationId, organizationUser);

        await RepositoryDeleteUserAsync(organizationUser, null);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Left);
    }

    private void ValidateDeleteUser(Guid organizationId, OrganizationUser orgUser)
    {
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }
    }

    private async Task RepositoryDeleteUserAsync(OrganizationUser orgUser, Guid? deletingUserId)
    {
        if (deletingUserId.HasValue && orgUser.UserId == deletingUserId.Value)
        {
            throw new BadRequestException("You cannot remove yourself.");
        }

        if (orgUser.Type == OrganizationUserType.Owner)
        {
            if (deletingUserId.HasValue && !await _currentContext.OrganizationOwner(orgUser.OrganizationId))
            {
                throw new BadRequestException("Only owners can delete other owners.");
            }

            if (!await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(orgUser.OrganizationId, new[] { orgUser.Id }, includeProvider: true))
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }
        }

        await _organizationUserRepository.DeleteAsync(orgUser);

        if (orgUser.UserId.HasValue)
        {
            await DeleteAndPushUserRegistrationAsync(orgUser.OrganizationId, orgUser.UserId.Value);
        }
    }

    private async Task<IEnumerable<KeyValuePair<string, DeviceType>>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => new KeyValuePair<string, DeviceType>(d.Id.ToString(), d.Type));
    }

    private async Task DeleteAndPushUserRegistrationAsync(Guid organizationId, Guid userId)
    {
        var devices = await GetUserDeviceIdsAsync(userId);
        await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(devices,
            organizationId.ToString());
        await _pushNotificationService.PushSyncOrgKeysAsync(userId);
    }
}
