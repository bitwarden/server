using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Repositories;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.UserKey.Implementations;

/// <inheritdoc />
/// <summary>
/// Instantiates a new <see cref="RotateUserAccountKeysCommand"/>
/// </summary>
/// <param name="_userService">Master password hash validation</param>
/// <param name="_userRepository">Updates user keys and re-encrypted data if needed</param>
/// <param name="_cipherRepository">Provides a method to update re-encrypted cipher data</param>
/// <param name="_folderRepository">Provides a method to update re-encrypted folder data</param>
/// <param name="_sendRepository">Provides a method to update re-encrypted send data</param>
/// <param name="_emergencyAccessRepository">Provides a method to update re-encrypted emergency access data</param>
/// <param name="_organizationUserRepository">Provides a method to update re-encrypted organization user data</param>
/// <param name="_deviceRepository">Provides a method to update re-encrypted device data</param>
/// <param name="_passwordHasher">Hashes the new master password</param>
/// <param name="_pushService">Logs out user from other devices after successful rotation</param>
/// <param name="_identityErrorDescriber">Provides a password mismatch error if master password hash validation fails</param>
/// <param name="_credentialRepository">Provides a method to update re-encrypted WebAuthn keys</param>
/// <param name="_userSignatureKeyPairRepository">Provides a method to update re-encrypted signature keys</param>
public class RotateUserAccountKeysCommand(
    IUserService _userService,
    IUserRepository _userRepository,
    ICipherRepository _cipherRepository,
    IFolderRepository _folderRepository,
    ISendRepository _sendRepository,
    IEmergencyAccessRepository _emergencyAccessRepository,
    IOrganizationUserRepository _organizationUserRepository,
    IDeviceRepository _deviceRepository,
    IPasswordHasher<User> _passwordHasher,
    IPushNotificationService _pushService,
    IdentityErrorDescriber _identityErrorDescriber,
    IWebAuthnCredentialRepository _credentialRepository,
    IUserSignatureKeyPairRepository _userSignatureKeyPairRepository
) : IRotateUserAccountKeysCommand
{
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

        List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions = [];

        UpdateAccountKeys(model, user, saveEncryptedDataActions);
        UpdateUnlockMethods(model, user, saveEncryptedDataActions);
        UpdateUserData(model, user, saveEncryptedDataActions);

        await _userRepository.UpdateUserKeyAndEncryptedDataV2Async(user, saveEncryptedDataActions);
        await _pushService.PushLogOutAsync(user.Id);
        return IdentityResult.Success;
    }

    async Task<bool> IsUserV2UserAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var currentSignatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id);
        return currentSignatureKeyPair != null;
    }

    async void ValidateRotationModelSignatureKeyPairForV2User(RotateUserAccountKeysData model, User user)
    {
        var currentSignatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id);
        if (model.AccountKeys.SignatureKeyPairData == null)
        {
            throw new InvalidOperationException("The provided signing key data is null, but the user already has signing keys.");
        }

        if (model.AccountKeys.SignatureKeyPairData.VerifyingKey != currentSignatureKeyPair.VerifyingKey)
        {
            throw new InvalidOperationException("The provided signing key data does not match the user's current signing key data.");
        }
        if (string.IsNullOrEmpty(model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey))
        {
            throw new InvalidOperationException("No signed public key provided, but the user already has a signature key pair.");
        }
    }

    void ValidateRotationModelSignatureKeyPairForV1UserAndUpgradeToV2(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        if (model.AccountKeys.SignatureKeyPairData != null)
        {
            // user is upgrading
            if (string.IsNullOrEmpty(model.AccountKeys.SignatureKeyPairData.VerifyingKey))
            {
                throw new InvalidOperationException("The provided signing key data does not contain a valid verifying key.");
            }

            if (string.IsNullOrEmpty(model.AccountKeys.SignatureKeyPairData.WrappedSigningKey))
            {
                throw new InvalidOperationException("The provided signing key data does not contain a valid wrapped signing key.");
            }
            saveEncryptedDataActions.Add(_userSignatureKeyPairRepository.SetUserSignatureKeyPair(user.Id, model.AccountKeys.SignatureKeyPairData));
            user.SignedPublicKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey;
        }
    }

    async void UpdateAccountKeys(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        // Changing the public key encryption key pair is not supported during key rotation for now; so this ensures it is not accidentally changed
        if (model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey != user.PublicKey)
        {
            throw new InvalidOperationException("The provided account public key does not match the user's current public key, and changing the account asymmetric keypair is currently not supported during key rotation.");
        }
        // Private key is re-wrapped with new user key by client
        user.PrivateKey = model.UserKeyEncryptedAccountPrivateKey;


        if (await IsUserV2UserAsync(user))
        {
            ValidateRotationModelSignatureKeyPairForV2User(model, user);
        }
        else
        {
            ValidateRotationModelSignatureKeyPairForV1UserAndUpgradeToV2(model, user, saveEncryptedDataActions);
        }
    }

    void UpdateUserData(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
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
    }

    void UpdateUnlockMethods(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        if (!model.MasterPasswordUnlockData.ValidateForUser(user))
        {
            throw new InvalidOperationException("The provided master password unlock data is not valid for this user.");
        }
        // Update master password authentication & unlock
        user.Key = model.MasterPasswordUnlockData.MasterKeyEncryptedUserKey;
        user.MasterPassword = _passwordHasher.HashPassword(user, model.MasterPasswordUnlockData.MasterKeyAuthenticationHash);
        user.MasterPasswordHint = model.MasterPasswordUnlockData.MasterPasswordHint;

        if (model.EmergencyAccesses.Any())
        {
            saveEncryptedDataActions.Add(_emergencyAccessRepository.UpdateForKeyRotation(user.Id, model.EmergencyAccesses));
        }

        if (model.OrganizationUsers.Any())
        {
            saveEncryptedDataActions.Add(_organizationUserRepository.UpdateForKeyRotation(user.Id, model.OrganizationUsers));
        }

        if (model.WebAuthnKeys.Any())
        {
            saveEncryptedDataActions.Add(_credentialRepository.UpdateKeysForRotationAsync(user.Id, model.WebAuthnKeys));
        }

        if (model.DeviceKeys.Any())
        {
            saveEncryptedDataActions.Add(_deviceRepository.UpdateKeysForRotationAsync(user.Id, model.DeviceKeys));
        }
    }
}
