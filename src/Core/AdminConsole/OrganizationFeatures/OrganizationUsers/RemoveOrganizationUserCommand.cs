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
    private readonly TimeProvider _timeProvider;

    public const string UserNotFoundErrorMessage = "User not found.";
    public const string UsersInvalidErrorMessage = "Users invalid.";
    public const string RemoveYourselfErrorMessage = "You cannot remove yourself.";
    public const string RemoveOwnerByNonOwnerErrorMessage = "Only owners can delete other owners.";
    public const string RemoveLastConfirmedOwnerErrorMessage = "Organization must have at least one confirmed owner.";
    public const string RemoveClaimedAccountErrorMessage = "Cannot remove member accounts claimed by the organization. To offboard a member, revoke or delete the account.";

    public RemoveOrganizationUserCommand(
        IDeviceRepository deviceRepository,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService,
        IPushNotificationService pushNotificationService,
        IPushRegistrationService pushRegistrationService,
        ICurrentContext currentContext,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
        IGetOrganizationUsersManagementStatusQuery getOrganizationUsersManagementStatusQuery,
        IFeatureService featureService,
        TimeProvider timeProvider)
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
        _timeProvider = timeProvider;
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        ValidateRemoveUser(organizationId, organizationUser);

        await RepositoryRemoveUserAsync(organizationUser, deletingUserId: null, eventSystemUser: null);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        ValidateRemoveUser(organizationId, organizationUser);

        await RepositoryRemoveUserAsync(organizationUser, deletingUserId, eventSystemUser: null);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        ValidateRemoveUser(organizationId, organizationUser);

        await RepositoryRemoveUserAsync(organizationUser, deletingUserId: null, eventSystemUser);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed, eventSystemUser);
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> RemoveUsersAsync(
        Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? deletingUserId)
    {
        var result = await RemoveUsersInternalAsync(organizationId, organizationUserIds, deletingUserId, eventSystemUser: null);

        var removedUsers = result.Where(r => string.IsNullOrEmpty(r.ErrorMessage)).Select(r => r.OrganizationUser).ToList();
        if (removedUsers.Any())
        {
            DateTime? eventDate = _timeProvider.GetUtcNow().UtcDateTime;
            await _eventService.LogOrganizationUserEventsAsync(
                removedUsers.Select(ou => (ou, EventType.OrganizationUser_Removed, eventDate)));
        }

        return result.Select(r => (r.OrganizationUser.Id, r.ErrorMessage));
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> RemoveUsersAsync(
        Guid organizationId, IEnumerable<Guid> organizationUserIds, EventSystemUser eventSystemUser)
    {
        var result = await RemoveUsersInternalAsync(organizationId, organizationUserIds, deletingUserId: null, eventSystemUser);

        var removedUsers = result.Where(r => string.IsNullOrEmpty(r.ErrorMessage)).Select(r => r.OrganizationUser).ToList();
        if (removedUsers.Any())
        {
            DateTime? eventDate = _timeProvider.GetUtcNow().UtcDateTime;
            await _eventService.LogOrganizationUserEventsAsync(
                removedUsers.Select(ou => (ou, EventType.OrganizationUser_Removed, eventSystemUser, eventDate)));
        }

        return result.Select(r => (r.OrganizationUser.Id, r.ErrorMessage));
    }

    private void ValidateRemoveUser(Guid organizationId, OrganizationUser orgUser)
    {
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException(UserNotFoundErrorMessage);
        }
    }

    private async Task RepositoryRemoveUserAsync(OrganizationUser orgUser, Guid? deletingUserId, EventSystemUser? eventSystemUser)
    {
        if (deletingUserId.HasValue && orgUser.UserId == deletingUserId.Value)
        {
            throw new BadRequestException(RemoveYourselfErrorMessage);
        }

        if (orgUser.Type == OrganizationUserType.Owner)
        {
            if (deletingUserId.HasValue && !await _currentContext.OrganizationOwner(orgUser.OrganizationId))
            {
                throw new BadRequestException(RemoveOwnerByNonOwnerErrorMessage);
            }

            if (!await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(orgUser.OrganizationId, new[] { orgUser.Id }, includeProvider: true))
            {
                throw new BadRequestException(RemoveLastConfirmedOwnerErrorMessage);
            }
        }

        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning) && deletingUserId.HasValue && eventSystemUser == null)
        {
            var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(orgUser.OrganizationId, new[] { orgUser.Id });
            if (managementStatus.TryGetValue(orgUser.Id, out var isManaged) && isManaged)
            {
                throw new BadRequestException(RemoveClaimedAccountErrorMessage);
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
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId).ToList();

        if (!filteredUsers.Any())
        {
            throw new BadRequestException(UsersInvalidErrorMessage);
        }

        if (!await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId))
        {
            throw new BadRequestException(RemoveLastConfirmedOwnerErrorMessage);
        }

        var deletingUserIsOwner = false;
        if (deletingUserId.HasValue)
        {
            deletingUserIsOwner = await _currentContext.OrganizationOwner(organizationId);
        }

        var managementStatus = _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning) && deletingUserId.HasValue && eventSystemUser == null
            ? await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, filteredUsers.Select(u => u.Id))
            : filteredUsers.ToDictionary(u => u.Id, u => false);
        var result = new List<(OrganizationUser OrganizationUser, string ErrorMessage)>();
        foreach (var orgUser in filteredUsers)
        {
            try
            {
                if (deletingUserId.HasValue && orgUser.UserId == deletingUserId)
                {
                    throw new BadRequestException(RemoveYourselfErrorMessage);
                }

                if (orgUser.Type == OrganizationUserType.Owner && deletingUserId.HasValue && !deletingUserIsOwner)
                {
                    throw new BadRequestException(RemoveOwnerByNonOwnerErrorMessage);
                }

                if (managementStatus.TryGetValue(orgUser.Id, out var isManaged) && isManaged)
                {
                    throw new BadRequestException(RemoveClaimedAccountErrorMessage);
                }

                result.Add((orgUser, string.Empty));
            }
            catch (BadRequestException e)
            {
                result.Add((orgUser, e.Message));
            }
        }

        var organizationUsersToRemove = result.Where(r => string.IsNullOrEmpty(r.ErrorMessage)).Select(r => r.OrganizationUser).ToList();
        if (organizationUsersToRemove.Any())
        {
            await _organizationUserRepository.DeleteManyAsync(organizationUsersToRemove.Select(ou => ou.Id));
            foreach (var orgUser in organizationUsersToRemove.Where(ou => ou.UserId.HasValue))
            {
                await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
            }
        }

        return result;
    }
}
