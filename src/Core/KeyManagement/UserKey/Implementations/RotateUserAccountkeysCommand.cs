// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
    private readonly IUserSignatureKeyPairRepository _userSignatureKeyPairRepository;

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
    /// <param name="deviceRepository">Provides a method to update re-encrypted device keys</param>
    /// <param name="passwordHasher">Hashes the new master password</param>
    /// <param name="pushService">Logs out user from other devices after successful rotation</param>
    /// <param name="errors">Provides a password mismatch error if master password hash validation fails</param>
    /// <param name="credentialRepository">Provides a method to update re-encrypted WebAuthn keys</param>
    /// <param name="userSignatureKeyPairRepository">Provides a method to update re-encrypted signature keys</param>
    public RotateUserAccountKeysCommand(IUserService userService, IUserRepository userRepository,
        ICipherRepository cipherRepository, IFolderRepository folderRepository, ISendRepository sendRepository,
        IEmergencyAccessRepository emergencyAccessRepository, IOrganizationUserRepository organizationUserRepository,
        IDeviceRepository deviceRepository,
        IPasswordHasher<User> passwordHasher,
        IPushNotificationService pushService, IdentityErrorDescriber errors, IWebAuthnCredentialRepository credentialRepository,
        IUserSignatureKeyPairRepository userSignatureKeyPairRepository)
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
        _userSignatureKeyPairRepository = userSignatureKeyPairRepository;
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

        List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions = [];

        await UpdateAccountKeysAsync(model, user, saveEncryptedDataActions);
        UpdateUnlockMethods(model, user, saveEncryptedDataActions);
        UpdateUserData(model, user, saveEncryptedDataActions);

        await _userRepository.UpdateUserKeyAndEncryptedDataV2Async(user, saveEncryptedDataActions);
        await _pushService.PushLogOutAsync(user.Id);
        return IdentityResult.Success;
    }

    public async Task RotateV2AccountKeysAsync(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        ValidateV2Encryption(model);
        await ValidateVerifyingKeyUnchangedAsync(model, user);

        saveEncryptedDataActions.Add(_userSignatureKeyPairRepository.UpdateForKeyRotation(user.Id, model.AccountKeys.SignatureKeyPairData));
        user.SignedPublicKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey;
        user.SecurityState = model.AccountKeys.SecurityStateData!.SecurityState;
    }

    public void UpgradeV1ToV2Keys(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        ValidateV2Encryption(model);
        saveEncryptedDataActions.Add(_userSignatureKeyPairRepository.SetUserSignatureKeyPair(user.Id, model.AccountKeys.SignatureKeyPairData));
        user.SignedPublicKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey;
        user.SecurityState = model.AccountKeys.SecurityStateData!.SecurityState;
    }

    public async Task UpdateAccountKeysAsync(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        ValidatePublicKeyEncryptionKeyPairUnchanged(model, user);

        if (IsV2EncryptionUserAsync(user))
        {
            await RotateV2AccountKeysAsync(model, user, saveEncryptedDataActions);
        }
        else if (model.AccountKeys.SignatureKeyPairData != null)
        {
            UpgradeV1ToV2Keys(model, user, saveEncryptedDataActions);
        }
        else
        {
            if (GetEncryptionType(model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey) != EncryptionType.AesCbc256_HmacSha256_B64)
            {
                throw new InvalidOperationException("The provided account private key was not wrapped with AES-256-CBC-HMAC");
            }
            // V1 user to V1 user rotation needs to further changes, the private key was re-encrypted.
        }

        // Private key is re-wrapped with new user key by client
        user.PrivateKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey;
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

    private bool IsV2EncryptionUserAsync(User user)
    {
        // Returns whether the user is a V2 user based on the private key's encryption type.
        ArgumentNullException.ThrowIfNull(user);
        var isPrivateKeyEncryptionV2 = GetEncryptionType(user.PrivateKey) == EncryptionType.XChaCha20Poly1305_B64;
        return isPrivateKeyEncryptionV2;
    }

    private async Task ValidateVerifyingKeyUnchangedAsync(RotateUserAccountKeysData model, User user)
    {
        var currentSignatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id) ?? throw new InvalidOperationException("User does not have a signature key pair.");
        if (model.AccountKeys.SignatureKeyPairData.VerifyingKey != currentSignatureKeyPair!.VerifyingKey)
        {
            throw new InvalidOperationException("The provided verifying key does not match the user's current verifying key.");
        }
    }

    private static void ValidatePublicKeyEncryptionKeyPairUnchanged(RotateUserAccountKeysData model, User user)
    {
        var publicKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey;
        if (publicKey != user.PublicKey)
        {
            throw new InvalidOperationException("The provided account public key does not match the user's current public key, and changing the account asymmetric key pair is currently not supported during key rotation.");
        }
    }

    private static void ValidateV2Encryption(RotateUserAccountKeysData model)
    {
        if (model.AccountKeys.SignatureKeyPairData == null)
        {
            throw new InvalidOperationException("Signature key pair data is required for V2 encryption.");
        }
        if (GetEncryptionType(model.AccountKeys.SignatureKeyPairData.WrappedSigningKey) != EncryptionType.XChaCha20Poly1305_B64)
        {
            throw new InvalidOperationException("The provided signing key data is not wrapped with XChaCha20-Poly1305.");
        }
        if (string.IsNullOrEmpty(model.AccountKeys.SignatureKeyPairData.VerifyingKey))
        {
            throw new InvalidOperationException("The provided signature key pair data does not contain a valid verifying key.");
        }

        if (GetEncryptionType(model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey) != EncryptionType.XChaCha20Poly1305_B64)
        {
            throw new InvalidOperationException("The provided private key encryption key is not wrapped with XChaCha20-Poly1305.");
        }
        if (string.IsNullOrEmpty(model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey))
        {
            throw new InvalidOperationException("No signed public key provided, but the user already has a signature key pair.");
        }
        if (model.AccountKeys.SecurityStateData == null || string.IsNullOrEmpty(model.AccountKeys.SecurityStateData.SecurityState))
        {
            throw new InvalidOperationException("No signed security state provider for V2 user");
        }
    }

    /// <summary>
    /// Helper method to convert an encryption type string to an enum value.
    /// </summary>
    private static EncryptionType GetEncryptionType(string encString)
    {
        var parts = encString.Split('.');
        if (parts.Length == 1)
        {
            throw new ArgumentException("Invalid encryption type string.");
        }
        if (byte.TryParse(parts[0], out var encryptionTypeNumber))
        {
            if (Enum.IsDefined(typeof(EncryptionType), encryptionTypeNumber))
            {
                return (EncryptionType)encryptionTypeNumber;
            }
        }
        throw new ArgumentException("Invalid encryption type string.");
    }
}
