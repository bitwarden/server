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
    private readonly IGetOrganizationUsersManagementStatusQuery _getOrganizationUsersManagementStatusQuery;
    private readonly IFeatureService _featureService;

    public RemoveOrganizationUserCommand(
        IDeviceRepository deviceRepository,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService,
        IPushNotificationService pushNotificationService,
        IPushRegistrationService pushRegistrationService,
        ICurrentContext currentContext,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
        IGetOrganizationUsersManagementStatusQuery getOrganizationUsersManagementStatusQuery,
        IFeatureService featureService)
    {
        _deviceRepository = deviceRepository;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _currentContext = currentContext;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
        _getOrganizationUsersManagementStatusQuery = getOrganizationUsersManagementStatusQuery;
        _featureService = featureService;
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        ValidateDeleteUser(organizationId, organizationUser);

        await RepositoryDeleteUserAsync(organizationUser, null, null);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        ValidateDeleteUser(organizationId, organizationUser);

        await RepositoryDeleteUserAsync(organizationUser, deletingUserId, null);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        ValidateDeleteUser(organizationId, organizationUser);

        await RepositoryDeleteUserAsync(organizationUser, null, eventSystemUser);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed, eventSystemUser);
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> RemoveUsersAsync(
        Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? deletingUserId)
    {
        var result = await RemoveUsersInternalAsync(organizationId, organizationUserIds, deletingUserId, null);

        DateTime? eventDate = DateTime.UtcNow;
        if (result.Any(r => r.ErrorMessage == string.Empty))
        {
            await _eventService.LogOrganizationUserEventsAsync(
                result.Where(r => r.ErrorMessage == string.Empty)
                    .Select(u => (u.OrganizationUser, EventType.OrganizationUser_Removed, eventDate)));
        }

        return result.Select(r => (r.OrganizationUser.Id, r.ErrorMessage));
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> RemoveUsersAsync(
        Guid organizationId, IEnumerable<Guid> organizationUserIds, EventSystemUser eventSystemUser)
    {
        var result = await RemoveUsersInternalAsync(organizationId, organizationUserIds, null, eventSystemUser);

        DateTime? eventDate = DateTime.UtcNow;
        if (result.Any(r => r.ErrorMessage == string.Empty))
        {
            await _eventService.LogOrganizationUserEventsAsync(
                result.Where(r => r.ErrorMessage == string.Empty)
                    .Select(u => (u.OrganizationUser, EventType.OrganizationUser_Removed, eventSystemUser, eventDate)));
        }

        return result.Select(r => (r.OrganizationUser.Id, r.ErrorMessage));
    }

    private void ValidateDeleteUser(Guid organizationId, OrganizationUser orgUser)
    {
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }
    }

    private async Task RepositoryDeleteUserAsync(OrganizationUser orgUser, Guid? deletingUserId, EventSystemUser? eventSystemUser)
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

        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning) && eventSystemUser == null)
        {
            var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(orgUser.OrganizationId, new[] { orgUser.Id });
            if (managementStatus.TryGetValue(orgUser.Id, out var isManaged) && isManaged)
            {
                throw new BadRequestException("Managed members cannot be simply removed, their entire individual account must be deleted.");
            }
        }

        await _organizationUserRepository.DeleteAsync(orgUser);

        if (orgUser.UserId.HasValue)
        {
            await DeleteAndPushUserRegistrationAsync(orgUser.OrganizationId, orgUser.UserId.Value);
        }
    }

    private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
    }

    private async Task DeleteAndPushUserRegistrationAsync(Guid organizationId, Guid userId)
    {
        var devices = await GetUserDeviceIdsAsync(userId);
        await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(devices,
            organizationId.ToString());
        await _pushNotificationService.PushSyncOrgKeysAsync(userId);
    }

    private async Task<IEnumerable<(OrganizationUser OrganizationUser, string ErrorMessage)>> RemoveUsersInternalAsync(
        Guid organizationId, IEnumerable<Guid> organizationUsersId, Guid? deletingUserId, EventSystemUser? eventSystemUser)
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

        var managementStatus = _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning) && eventSystemUser == null
            ? await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, filteredUsers.Select(u => u.Id))
            : filteredUsers.ToDictionary(u => u.Id, u => false);
        var result = new List<(OrganizationUser OrganizationUser, string ErrorMessage)>();
        var organizationUserIdsToDelete = new List<Guid>();
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

                if (managementStatus.TryGetValue(orgUser.Id, out var isManaged) && isManaged)
                {
                    throw new BadRequestException("Managed members cannot be simply removed, their entire individual account must be deleted.");
                }

                organizationUserIdsToDelete.Add(orgUser.Id);
                result.Add((orgUser, string.Empty));
            }
            catch (BadRequestException e)
            {
                result.Add((orgUser, e.Message));
            }
        }

        if (organizationUserIdsToDelete.Any())
        {
            DateTime? eventDate = DateTime.UtcNow;
            await _organizationUserRepository.DeleteManyAsync(organizationUserIdsToDelete);
            foreach (var orgUser in filteredUsers.Where(u => organizationUserIdsToDelete.Contains(u.Id) && u.UserId.HasValue))
            {
                await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
            }
        }

        return result;
    }
}
