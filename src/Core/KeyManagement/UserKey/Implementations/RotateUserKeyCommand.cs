using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Repositories;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.UserKey.Implementations;

/// <inheritdoc />
public class RotateUserKeyCommand : IRotateUserKeyCommand
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly ISendRepository _sendRepository;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IdentityErrorDescriber _identityErrorDescriber;
    private readonly IWebAuthnCredentialRepository _credentialRepository;

    /// <summary>
    /// Instantiates a new <see cref="RotateUserKeyCommand"/>
    /// </summary>
    /// <param name="userService">Master password hash validation</param>
    /// <param name="userRepository">Updates user keys and re-encrypted data if needed</param>
    /// <param name="cipherRepository">Provides a method to update re-encrypted cipher data</param>
    /// <param name="folderRepository">Provides a method to update re-encrypted folder data</param>
    /// <param name="sendRepository">Provides a method to update re-encrypted send data</param>
    /// <param name="emergencyAccessRepository">Provides a method to update re-encrypted emergency access data</param>
    /// <param name="pushService">Logs out user from other devices after successful rotation</param>
    /// <param name="errors">Provides a password mismatch error if master password hash validation fails</param>
    public RotateUserKeyCommand(IUserService userService, IUserRepository userRepository,
        ICipherRepository cipherRepository, IFolderRepository folderRepository, ISendRepository sendRepository,
        IEmergencyAccessRepository emergencyAccessRepository, IOrganizationUserRepository organizationUserRepository,
        IPushNotificationService pushService, IdentityErrorDescriber errors, IWebAuthnCredentialRepository credentialRepository)
    {
        _userService = userService;
        _userRepository = userRepository;
        _cipherRepository = cipherRepository;
        _folderRepository = folderRepository;
        _sendRepository = sendRepository;
        _emergencyAccessRepository = emergencyAccessRepository;
        _organizationUserRepository = organizationUserRepository;
        _pushService = pushService;
        _identityErrorDescriber = errors;
        _credentialRepository = credentialRepository;
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
        if (model.Ciphers.Any() || model.Folders.Any() || model.Sends.Any() || model.EmergencyAccesses.Any() ||
            model.OrganizationUsers.Any() || model.WebAuthnKeys.Any())
        {
            List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions = new();

            if (model.Ciphers.Any())
            {
                saveEncryptedDataActions.Add(_cipherRepository.UpdateForKeyRotation(user.Id, model.Ciphers));
            }

            if (model.Folders.Any())
            {
                saveEncryptedDataActions.Add(_folderRepository.UpdateForKeyRotation(user.Id, model.Folders));
            }

            if (model.Sends.Any())
            {
                saveEncryptedDataActions.Add(_sendRepository.UpdateForKeyRotation(user.Id, model.Sends));
            }

            if (model.EmergencyAccesses.Any())
            {
                saveEncryptedDataActions.Add(
                    _emergencyAccessRepository.UpdateForKeyRotation(user.Id, model.EmergencyAccesses));
            }

            if (model.OrganizationUsers.Any())
            {
                saveEncryptedDataActions.Add(
                    _organizationUserRepository.UpdateForKeyRotation(user.Id, model.OrganizationUsers));
            }

            if (model.WebAuthnKeys.Any())
            {
                saveEncryptedDataActions.Add(_credentialRepository.UpdateKeysForRotationAsync(user.Id, model.WebAuthnKeys));
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
