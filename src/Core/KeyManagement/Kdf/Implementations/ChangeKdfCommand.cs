using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Core.KeyManagement.Kdf.Implementations;

/// <inheritdoc />
public class ChangeKdfCommand : IChangeKdfCommand
{
    private readonly IUserService _userService;
    private readonly IPushNotificationService _pushService;
    private readonly IUserRepository _userRepository;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IdentityErrorDescriber _identityErrorDescriber;
    private readonly ILogger<ChangeKdfCommand> _logger;
    private readonly IFeatureService _featureService;

    public ChangeKdfCommand(IUserService userService, IPushNotificationService pushService,
        IUserRepository userRepository, IMasterPasswordService masterPasswordService, IdentityErrorDescriber describer,
        ILogger<ChangeKdfCommand> logger, IFeatureService featureService)
    {
        _userService = userService;
        _pushService = pushService;
        _userRepository = userRepository;
        _masterPasswordService = masterPasswordService;
        _identityErrorDescriber = describer;
        _logger = logger;
        _featureService = featureService;
    }

    public async Task<IdentityResult> ChangeKdfAsync(User user, string masterPasswordAuthenticationHash,
        MasterPasswordAuthenticationData authenticationData, MasterPasswordUnlockData unlockData)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!await _userService.CheckPasswordAsync(user, masterPasswordAuthenticationHash))
        {
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        // Validate to prevent user account from becoming un-decryptable from invalid parameters
        //
        // Prevent a de-synced salt value from creating an un-decryptable unlock method
        authenticationData.ValidateSaltUnchangedForUser(user);
        unlockData.ValidateSaltUnchangedForUser(user);

        // Currently KDF settings are not saved separately for authentication and unlock and must therefore be equal
        if (!authenticationData.Kdf.Equals(unlockData.Kdf))
        {
            throw new BadRequestException("KDF settings must be equal for authentication and unlock.");
        }

        var validationErrors = KdfSettingsValidator.Validate(unlockData.Kdf);
        if (validationErrors.Any())
        {
            throw new BadRequestException("KDF settings are invalid.");
        }

        var logoutOnKdfChange = !_featureService.IsEnabled(FeatureFlagKeys.NoLogoutOnKdfChange);

        // KM do we want this to be a new call in the master password service for ChangeKdf?
        var updateExisingPasswordResult = await _masterPasswordService.SaveUpdateExistingMasterPasswordAsync(user,
            new UpdateExistingPasswordData
            {
                MasterPasswordUnlock = unlockData,
                MasterPasswordAuthentication = authenticationData,
                RefreshStamp = logoutOnKdfChange
            });

        if (!updateExisingPasswordResult.Succeeded)
        {
            _logger.LogWarning("Change KDF failed for user {userId}.", user.Id);
            return updateExisingPasswordResult;
        }

        if (logoutOnKdfChange)
        {
            await _pushService.PushLogOutAsync(user.Id);
        }
        else
        {
            // Clients that support the new feature flag will ignore the logout when it matches the reason and the feature flag is enabled.
            await _pushService.PushLogOutAsync(user.Id, reason: PushNotificationLogOutReason.KdfChange);
            await _pushService.PushSyncSettingsAsync(user.Id);
        }

        return IdentityResult.Success;
    }
}
