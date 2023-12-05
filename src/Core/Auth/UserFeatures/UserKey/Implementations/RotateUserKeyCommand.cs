using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserKey.Implementations;

public class RotateUserKeyCommand : IRotateUserKeyCommand
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IdentityErrorDescriber _identityErrorDescriber;

    public RotateUserKeyCommand(IUserService userService, IUserRepository userRepository,
        IEmergencyAccessRepository emergencyAccessRepository,
        IPushNotificationService pushService, IdentityErrorDescriber errors)
    {
        _userService = userService;
        _userRepository = userRepository;
        _emergencyAccessRepository = emergencyAccessRepository;
        _pushService = pushService;
        _identityErrorDescriber = errors;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> RotateUserKeyAsync(User user, RotateUserKeyData model)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (!await _userService.CheckPasswordAsync(user, model.MasterPasswordHash))
        {
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        var now = DateTime.UtcNow;
        user.RevisionDate = user.AccountRevisionDate = now;
        user.LastKeyRotationDate = now;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.Key = model.Key;
        user.PrivateKey = model.PrivateKey;
        if (model.Ciphers.Any() || model.Folders.Any() || model.Sends.Any() || model.EmergencyAccessKeys.Any() ||
            model.ResetPasswordKeys.Any())
        {
            List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions = new();
            // if (model.Ciphers.Any())
            // {
            //      saveEncryptedDataActions.Add(_cipherRepository.SaveRotatedData);
            // }
            if (model.EmergencyAccessKeys.Any())
            {
                saveEncryptedDataActions.Add(
                    _emergencyAccessRepository.UpdateForKeyRotation(user.Id, model.EmergencyAccessKeys));
            }

            await _userRepository.UpdateUserKeyAndEncryptedDataAsync(user, saveEncryptedDataActions);
        }
        else
        {
            await _userRepository.ReplaceAsync(user);
        }

        await _pushService.PushLogOutAsync(user.Id, excludeCurrentContextFromPush: true);
        return IdentityResult.Success;
    }
}
