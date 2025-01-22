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
using Microsoft.Extensions.Logging;

namespace Bit.Core.KeyManagement.Commands;

public class RegenerateUserAsymmetricKeysCommand : IRegenerateUserAsymmetricKeysCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<RegenerateUserAsymmetricKeysCommand> _logger;
    private readonly IUserAsymmetricKeysRepository _userAsymmetricKeysRepository;
    private readonly IPushNotificationService _pushService;

    public RegenerateUserAsymmetricKeysCommand(
        ICurrentContext currentContext,
        IUserAsymmetricKeysRepository userAsymmetricKeysRepository,
        IPushNotificationService pushService,
        ILogger<RegenerateUserAsymmetricKeysCommand> logger)
    {
        _currentContext = currentContext;
        _logger = logger;
        _userAsymmetricKeysRepository = userAsymmetricKeysRepository;
        _pushService = pushService;
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

        // For now, don't regenerate asymmetric keys for user's with organization membership and designated emergency access.
        if (inOrganizations || hasDesignatedEmergencyAccess)
        {
            throw new BadRequestException("Key regeneration not supported for this user.");
        }

        await _userAsymmetricKeysRepository.RegenerateUserAsymmetricKeysAsync(userAsymmetricKeys);
        _logger.LogInformation(
            "User's asymmetric keys regenerated. UserId: {userId} OrganizationMembership: {inOrganizations} DesignatedEmergencyAccess: {hasDesignatedEmergencyAccess} DeviceType: {deviceType}",
            userAsymmetricKeys.UserId, inOrganizations, hasDesignatedEmergencyAccess, _currentContext.DeviceType);

        await _pushService.PushSyncSettingsAsync(userId.Value);
    }
}
