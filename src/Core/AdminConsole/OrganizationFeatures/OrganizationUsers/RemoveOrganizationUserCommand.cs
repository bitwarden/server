// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
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
    private readonly IGetOrganizationUsersClaimedStatusQuery _getOrganizationUsersClaimedStatusQuery;
    private readonly IFeatureService _featureService;
    private readonly TimeProvider _timeProvider;
    private readonly IOrganizationUserValidationService _organizationUserValidationService;

    public const string UserNotFoundErrorMessage = "User not found.";
    public static readonly string UsersInvalidErrorMessage = new UsersInvalid().Message;
    public const string RemoveYourselfErrorMessage = "You cannot remove yourself.";
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
        IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery,
        IFeatureService featureService,
        TimeProvider timeProvider,
        IOrganizationUserValidationService organizationUserValidationService)
    {
        _deviceRepository = deviceRepository;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _currentContext = currentContext;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
        _getOrganizationUsersClaimedStatusQuery = getOrganizationUsersClaimedStatusQuery;
        _featureService = featureService;
        _timeProvider = timeProvider;
        _organizationUserValidationService = organizationUserValidationService;
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

    public async Task UserLeaveAsync(Guid organizationId, Guid userId)
    {
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        ValidateRemoveUser(organizationId, organizationUser);

        await RepositoryRemoveUserAsync(organizationUser, deletingUserId: null, eventSystemUser: null);

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Left);
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

        if (deletingUserId.HasValue)
        {
            var actingOrganization = _currentContext.GetOrganization(orgUser.OrganizationId);
            var actingUser = actingOrganization is null
                ? null
                : new OrganizationUserRole(actingOrganization.Type, orgUser.OrganizationId, actingOrganization.Permissions);

            var error = await _organizationUserValidationService.CanManageAsync(deletingUserId.Value, actingUser, orgUser);
            if (error is not null)
            {
                throw new BadRequestException(error.Message);
            }
        }

        if (orgUser.Type == OrganizationUserType.Owner &&
            !await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(orgUser.OrganizationId, new[] { orgUser.Id }, includeProvider: true))
        {
            throw new BadRequestException(RemoveLastConfirmedOwnerErrorMessage);
        }

        if (deletingUserId.HasValue && eventSystemUser == null)
        {
            var claimedStatus = await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, new[] { orgUser.Id });
            if (claimedStatus.TryGetValue(orgUser.Id, out var isClaimed) && isClaimed)
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

    /// <summary>
    /// Resolves the "can the acting user manage this target's role" decision once for the whole batch via
    /// <see cref="IOrganizationUserValidationService"/>, instead of precomputing the acting user's org-level status
    /// and re-checking it per target.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Error>> GetManageErrorsAsync(
        Guid organizationId, Guid? deletingUserId, ICollection<OrganizationUser> targetUsers)
    {
        if (!deletingUserId.HasValue || targetUsers.Count == 0)
        {
            return new Dictionary<Guid, Error>();
        }

        var actingOrganization = _currentContext.GetOrganization(organizationId);
        var actingUser = actingOrganization is null
            ? null
            : new OrganizationUserRole(actingOrganization.Type, organizationId, actingOrganization.Permissions);

        var targetsById = targetUsers.ToDictionary(u => u.Id, u => (IOrganizationUserRole)u);

        var results = await _organizationUserValidationService.CanManageAsync(
            deletingUserId.Value, actingUser, organizationId, targetsById);

        return results
            .Where(kvp => kvp.Value is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);
    }

    private async Task<IEnumerable<(OrganizationUser OrganizationUser, string ErrorMessage)>> RemoveUsersInternalAsync(
        Guid organizationId, IEnumerable<Guid> organizationUsersId, Guid? deletingUserId, EventSystemUser? eventSystemUser)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUsersId);
        var filteredUsers = orgUsers.Where(u => u.OrganizationId == organizationId).ToList();

        if (!filteredUsers.Any())
        {
            throw new BadRequestException(new UsersInvalid().Message);
        }

        if (!await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId))
        {
            throw new BadRequestException(RemoveLastConfirmedOwnerErrorMessage);
        }

        var manageErrorsByOrgUserId = await GetManageErrorsAsync(organizationId, deletingUserId, filteredUsers);

        var claimedStatus = deletingUserId.HasValue && eventSystemUser == null
            ? await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(organizationId, filteredUsers.Select(u => u.Id))
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

                if (manageErrorsByOrgUserId.TryGetValue(orgUser.Id, out var manageError) && manageError is not null)
                {
                    throw new BadRequestException(manageError.Message);
                }

                if (claimedStatus.TryGetValue(orgUser.Id, out var isClaimed) && isClaimed)
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
                await DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId!.Value);
            }
        }

        return result;
    }
}
