using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.Kdf.Implementations;

/// <inheritdoc />
public class ChangeKdfCommand : IChangeKdfCommand
{
    private readonly IUserService _userService;
    private readonly IPushNotificationService _pushService;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IdentityErrorDescriber _identityErrorDescriber;
    private readonly IFeatureService _featureService;

    public ChangeKdfCommand(IUserService userService, IPushNotificationService pushService,
        IMasterPasswordService masterPasswordService, IdentityErrorDescriber describer,
        IFeatureService featureService)
    {
        _userService = userService;
        _pushService = pushService;
        _masterPasswordService = masterPasswordService;
        _identityErrorDescriber = describer;
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
        // Prevent a de-synced salt value from creating an un-decryptable unlock method.
        // Also checked in the MasterPasswordService via UpdateExistingKdfConfigurationData.ValidateDataForUser.
        authenticationData.ValidateSaltUnchangedForUser(user);
        unlockData.ValidateSaltUnchangedForUser(user);

        // Currently KDF settings are not saved separately for authentication and unlock and must therefore be equal
        if (!authenticationData.Kdf.Equals(unlockData.Kdf))
        {
            throw new BadRequestException("AuthenticationData and UnlockData must have the same KDF configuration.");
        }

        var validationErrors = KdfSettingsValidator.Validate(unlockData.Kdf);
        if (validationErrors.Any())
        {
            throw new BadRequestException("KDF settings are invalid.");
        }

        var logoutOnKdfChange = !_featureService.IsEnabled(FeatureFlagKeys.NoLogoutOnKdfChange);

        var data = new UpdateExistingKdfConfigurationData
        {
            MasterPasswordAuthentication = authenticationData,
            MasterPasswordUnlock = unlockData,
            ValidatePassword = false, // password already verified by CheckPasswordAsync above
            RefreshStamp = logoutOnKdfChange,
            MasterPasswordHint = user.MasterPasswordHint, // KDF rotation does not change the hint; carry existing value through
        };

        var result = await _masterPasswordService.SaveUpdateExistingKdfConfigurationAsync(user, data);
        if (result.TryPickT1(out var errors, out _))
        {
            return IdentityResult.Failed(errors);
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
