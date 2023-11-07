using Bit.Core.Auth.UserFeatures.UserKey.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserKey;

public class RotateUserKeyCommand : IRotateUserKeyCommand
{

    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IdentityErrorDescriber _identityErrorDescriber;

    public RotateUserKeyCommand(IUserService userService, IUserRepository userRepository, IPushNotificationService pushService, IdentityErrorDescriber errors)
    {
        _userService = userService;
        _userRepository = userRepository;
        _pushService = pushService;
        _identityErrorDescriber = errors;

    }

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
        if (model.Ciphers.Any() || model.Folders.Any() || model.Sends.Any() || model.EmergencyAccessKeys.Any() || model.AccountRecoveryKeys.Any())
        {
            await _userRepository.UpdateUserKeyAndEncryptedDataAsync(user, model.Ciphers, model.Folders, model.Sends,
                model.EmergencyAccessKeys, model.AccountRecoveryKeys);
        }
        else
        {
            await _userRepository.ReplaceAsync(user);
        }

        await _pushService.PushLogOutAsync(user.Id, excludeCurrentContextFromPush: true);
        return IdentityResult.Success;
    }
}
