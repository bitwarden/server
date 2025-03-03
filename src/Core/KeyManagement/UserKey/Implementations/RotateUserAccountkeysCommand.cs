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
public class RotateUserAccountKeysCommand : IRotateUserAccountKeysCommand
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly ISendRepository _sendRepository;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IdentityErrorDescriber _identityErrorDescriber;
    private readonly IWebAuthnCredentialRepository _credentialRepository;
    private readonly IPasswordHasher<User> _passwordHasher;

    /// <summary>
    /// Instantiates a new <see cref="RotateUserAccountKeysCommand"/>
    /// </summary>
    /// <param name="userService">Master password hash validation</param>
    /// <param name="userRepository">Updates user keys and re-encrypted data if needed</param>
    /// <param name="cipherRepository">Provides a method to update re-encrypted cipher data</param>
    /// <param name="folderRepository">Provides a method to update re-encrypted folder data</param>
    /// <param name="sendRepository">Provides a method to update re-encrypted send data</param>
    /// <param name="emergencyAccessRepository">Provides a method to update re-encrypted emergency access data</param>
    /// <param name="organizationUserRepository">Provides a method to update re-encrypted organization user data</param>
    /// <param name="passwordHasher">Hashes the new master password</param>
    /// <param name="pushService">Logs out user from other devices after successful rotation</param>
    /// <param name="errors">Provides a password mismatch error if master password hash validation fails</param>
    /// <param name="credentialRepository">Provides a method to update re-encrypted WebAuthn keys</param>
    public RotateUserAccountKeysCommand(IUserService userService, IUserRepository userRepository,
        ICipherRepository cipherRepository, IFolderRepository folderRepository, ISendRepository sendRepository,
        IEmergencyAccessRepository emergencyAccessRepository, IOrganizationUserRepository organizationUserRepository,
        IDeviceRepository deviceRepository,
        IPasswordHasher<User> passwordHasher,
        IPushNotificationService pushService, IdentityErrorDescriber errors, IWebAuthnCredentialRepository credentialRepository)
    {
        _userService = userService;
        _userRepository = userRepository;
        _cipherRepository = cipherRepository;
        _folderRepository = folderRepository;
        _sendRepository = sendRepository;
        _emergencyAccessRepository = emergencyAccessRepository;
        _organizationUserRepository = organizationUserRepository;
        _deviceRepository = deviceRepository;
        _pushService = pushService;
        _identityErrorDescriber = errors;
        _credentialRepository = credentialRepository;
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> RotateUserAccountKeysAsync(User user, RotateUserAccountKeysData model)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (!await _userService.CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash))
        {
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        var now = DateTime.UtcNow;
        user.RevisionDate = user.AccountRevisionDate = now;
        user.LastKeyRotationDate = now;
        user.SecurityStamp = Guid.NewGuid().ToString();

        if (
            !model.MasterPasswordUnlockData.ValidateForUser(user)
        )
        {
            throw new InvalidOperationException("The provided master password unlock data is not valid for this user.");
        }
        if (
            model.AccountPublicKey != user.PublicKey
        )
        {
            throw new InvalidOperationException("The provided account public key does not match the user's current public key, and changing the account asymmetric keypair is currently not supported during key rotation.");
        }

        user.Key = model.MasterPasswordUnlockData.MasterKeyEncryptedUserKey;
        user.PrivateKey = model.UserKeyEncryptedAccountPrivateKey;
        user.MasterPassword = _passwordHasher.HashPassword(user, model.MasterPasswordUnlockData.MasterKeyAuthenticationHash);
        user.MasterPasswordHint = model.MasterPasswordUnlockData.MasterPasswordHint;

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

        if (model.DeviceKeys.Any())
        {
            saveEncryptedDataActions.Add(_deviceRepository.UpdateKeysForRotationAsync(user.Id, model.DeviceKeys));
        }

        await _userRepository.UpdateUserKeyAndEncryptedDataV2Async(user, saveEncryptedDataActions);
        await _pushService.PushLogOutAsync(user.Id);
        return IdentityResult.Success;
    }
}
