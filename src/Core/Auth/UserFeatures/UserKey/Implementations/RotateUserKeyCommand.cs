using Bit.Core.Auth.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserKey.Implementations;

public class RotateUserKeyCommand : IRotateUserKeyCommand
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IdentityErrorDescriber _identityErrorDescriber;

    public RotateUserKeyCommand(IUserService userService, IUserRepository userRepository,
        IPushNotificationService pushService, IdentityErrorDescriber errors)
    {
        _userService = userService;
        _userRepository = userRepository;
        _pushService = pushService;
        _identityErrorDescriber = errors;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> RotateUserKeyAsync(RotateUserKeyData model)
    {
        if (model.User == null)
        {
            throw new ArgumentNullException(nameof(model.User));
        }

        if (!await _userService.CheckPasswordAsync(model.User, model.MasterPasswordHash))
        {
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        var now = DateTime.UtcNow;
        model.User.RevisionDate = model.User.AccountRevisionDate = now;
        model.User.LastKeyRotationDate = now;
        model.User.SecurityStamp = Guid.NewGuid().ToString();
        model.User.Key = model.Key;
        model.User.PrivateKey = model.PrivateKey;
        if (model.Ciphers.Any() || model.Folders.Any() || model.Sends.Any() || model.EmergencyAccessKeys.Any() ||
            model.ResetPasswordKeys.Any())
        {
            await _userRepository.UpdateUserKeyAndEncryptedDataAsync(model.User, model.Ciphers, model.Folders,
                model.Sends, model.EmergencyAccessKeys, model.ResetPasswordKeys);
        }
        else
        {
            await _userRepository.ReplaceAsync(model.User);
        }

        await _pushService.PushLogOutAsync(model.User.Id, excludeCurrentContextFromPush: true);
        return IdentityResult.Success;
    }
}
