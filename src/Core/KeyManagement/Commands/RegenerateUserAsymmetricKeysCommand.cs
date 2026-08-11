#nullable enable
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.KeyManagement.Commands;

public class RegenerateUserAsymmetricKeysCommand : IRegenerateUserAsymmetricKeysCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<RegenerateUserAsymmetricKeysCommand> _logger;
    private readonly IUserAsymmetricKeysRepository _userAsymmetricKeysRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;
    private readonly IMailService _mailService;

    public RegenerateUserAsymmetricKeysCommand(
        ICurrentContext currentContext,
        IUserAsymmetricKeysRepository userAsymmetricKeysRepository,
        IPushNotificationService pushService,
        ILogger<RegenerateUserAsymmetricKeysCommand> logger,
        IEmergencyAccessRepository emergencyAccessRepository,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService,
        IMailService mailService)
    {
        _currentContext = currentContext;
        _logger = logger;
        _userAsymmetricKeysRepository = userAsymmetricKeysRepository;
        _pushService = pushService;
        _emergencyAccessRepository = emergencyAccessRepository;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
        _mailService = mailService;
    }

    public async Task RegenerateKeysAsync(UserAsymmetricKeys userAsymmetricKeys,
        ICollection<OrganizationUser> usersOrganizationAccounts,
        ICollection<EmergencyAccessDetails> designatedEmergencyAccess)
    {
        var userId = _currentContext.UserId;
        if (!userId.HasValue ||
            userAsymmetricKeys.UserId != userId.Value ||
            usersOrganizationAccounts.Any(ou => ou.UserId != userId) ||
            designatedEmergencyAccess.Any(dea => dea.GranteeId != userId))
        {
            throw new NotFoundException();
        }

        var inOrganizations = usersOrganizationAccounts.Any(ou =>
            ou.Status is OrganizationUserStatusType.Confirmed or OrganizationUserStatusType.Revoked);
        var hasDesignatedEmergencyAccess = designatedEmergencyAccess.Any(x =>
            x.Status is EmergencyAccessStatusType.Confirmed or EmergencyAccessStatusType.RecoveryApproved
                or EmergencyAccessStatusType.RecoveryInitiated);

        _logger.LogInformation(
            "User asymmetric keys regeneration requested. UserId: {userId} OrganizationMembership: {inOrganizations} DesignatedEmergencyAccess: {hasDesignatedEmergencyAccess} DeviceType: {deviceType}",
            userAsymmetricKeys.UserId, inOrganizations, hasDesignatedEmergencyAccess, _currentContext.DeviceType);

        var updateDataActions = new List<DatabaseTransactionAction>();
        var utcNow = DateTime.UtcNow;

        var eaToReset = designatedEmergencyAccess
            .Where(ea => ea.Status is EmergencyAccessStatusType.Confirmed
                or EmergencyAccessStatusType.RecoveryInitiated
                or EmergencyAccessStatusType.RecoveryApproved)
            .ToList();
        foreach (var ea in eaToReset)
        {
            updateDataActions.Add(_emergencyAccessRepository.UpdateStatusAndKeyEncryptedById(
                ea.Id, EmergencyAccessStatusType.Accepted, null, utcNow));
        }

        var orgUsersToReset = usersOrganizationAccounts
            .Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
            .ToList();
        foreach (var orgUser in orgUsersToReset)
        {
            updateDataActions.Add(_organizationUserRepository.UpdateStatusAndKeyById(
                orgUser.Id, OrganizationUserStatusType.Accepted, null, utcNow));
        }

        var orgUsersToRemove = usersOrganizationAccounts
            .Where(ou => ou.Status == OrganizationUserStatusType.Revoked)
            .ToList();
        if (orgUsersToRemove.Count > 0)
        {
            updateDataActions.Add(
                _organizationUserRepository.DeleteManyByIds(orgUsersToRemove.Select(ou => ou.Id)));
        }

        await _userAsymmetricKeysRepository.RegenerateUserAsymmetricKeysAsync(
            userAsymmetricKeys, updateDataActions);

        _logger.LogInformation(
            "User's asymmetric keys regenerated. UserId: {userId} OrganizationMembership: {inOrganizations} DesignatedEmergencyAccess: {hasDesignatedEmergencyAccess} DeviceType: {deviceType}",
            userAsymmetricKeys.UserId, inOrganizations, hasDesignatedEmergencyAccess, _currentContext.DeviceType);

        await _pushService.PushSyncSettingsAsync(userId.Value);

        foreach (var orgUser in orgUsersToRemove)
        {
            await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Left);
        }

        foreach (var ea in eaToReset)
        {
            if (ea.GranteeEmail is null || ea.GrantorEmail is null)
            {
                continue;
            }

            await _mailService.SendEmergencyAccessAcceptedEmailAsync(
                ea.GranteeEmail, ea.GrantorEmail);
        }
    }
}
