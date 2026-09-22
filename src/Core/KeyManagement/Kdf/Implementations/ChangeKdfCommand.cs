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

    public ChangeKdfCommand(IUserService userService, IPushNotificationService pushService,
        IMasterPasswordService masterPasswordService, IdentityErrorDescriber describer)
    {
        _userService = userService;
        _pushService = pushService;
        _masterPasswordService = masterPasswordService;
        _identityErrorDescriber = describer;
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
        unlockData.ValidateKeyIdUnchangedForUser(user);

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

        var data = new UpdateExistingKdfConfigurationData
        {
            MasterPasswordAuthentication = authenticationData,
            MasterPasswordUnlock = unlockData,
            ValidatePassword = true,
            RefreshStamp = false,
            MasterPasswordHint = user.MasterPasswordHint, // KDF rotation does not change the hint; carry existing value through
        };

        var result = await _masterPasswordService.SaveUpdateExistingKdfConfigurationAsync(user, data);
        if (result.TryPickT1(out var errors, out _))
        {
            return IdentityResult.Failed(errors);
        }

        // Clients that don't recognize the KdfChange reason will log out; newer clients ignore it and sync settings instead.
        await _pushService.PushLogOutAsync(user.Id, reason: PushNotificationLogOutReason.KdfChange);
        await _pushService.PushSyncSettingsAsync(user.Id);

        return IdentityResult.Success;
    }
}
