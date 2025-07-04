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

        await UpdateAccountKeys(model, user, saveEncryptedDataActions);
        UpdateUnlockMethods(model, user, saveEncryptedDataActions);
        UpdateUserData(model, user, saveEncryptedDataActions);

        await _userRepository.UpdateUserKeyAndEncryptedDataV2Async(user, saveEncryptedDataActions);
        await _pushService.PushLogOutAsync(user.Id);
        return IdentityResult.Success;
    }

    async Task<bool> IsV2EncryptionUserAsync(User user)
    {
        // A V2 user has a signature key pair, and their user key is COSE
        // The user key cannot be directly checked here; but the items encrypted with it can be checked.
        ArgumentNullException.ThrowIfNull(user);
        var hasSignatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id) != null;
        var isPrivateKeyEncryptionV2 = GetEncryptionType(user.PrivateKey) == EncryptionType.XChaCha20Poly1305_B64;

        // Valid v2 user
        if (hasSignatureKeyPair && isPrivateKeyEncryptionV2)
        {
            return true;
        }

        // Valid v1 user
        if (!hasSignatureKeyPair && !isPrivateKeyEncryptionV2)
        {
            return false;
        }

        throw new InvalidOperationException("User is in an invalid state for key rotation. User has a signature key pair, but the private key is not in v2 format, or vice versa.");
    }

    public async Task RotateV2Keys(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        var currentSignatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id);
        if (model.AccountKeys == null || model.AccountKeys.SignatureKeyPairData == null)
        {
            throw new InvalidOperationException("The provided signing key data is null, but the user already has signing keys.");
        }
        if (model.AccountKeys.SignatureKeyPairData.VerifyingKey != currentSignatureKeyPair.VerifyingKey)
        {
            throw new InvalidOperationException("The provided verifying key does not match the user's current verifying key.");
        }
        if (model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey != user.PublicKey)
        {
            throw new InvalidOperationException("The provided public key encryption key pair data does not match the user's current public key.");
        }
        if (string.IsNullOrEmpty(model.AccountKeys.PublicKeyEncryptionKeyPairData?.SignedPublicKey))
        {
            throw new InvalidOperationException("No signed public key provided, but the user already has a signature key pair.");
        }
        if (GetEncryptionType(model.AccountKeys.SignatureKeyPairData.WrappedSigningKey) != EncryptionType.XChaCha20Poly1305_B64)
        {
            throw new InvalidOperationException("The provided signing key data is not wrapped with XChaCha20-Poly1305.");
        }
        if (GetEncryptionType(model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey) != EncryptionType.XChaCha20Poly1305_B64)
        {
            throw new InvalidOperationException("The provided public key encryption private key data is not wrapped with XChaCha20-Poly1305.");
        }

        saveEncryptedDataActions.Add(_userSignatureKeyPairRepository.UpdateForKeyRotation(user.Id, model.AccountKeys.SignatureKeyPairData));
        user.PrivateKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey;
        user.SignedPublicKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey;
        user.SecurityState = model.AccountKeys.SecurityStateData.SecurityState;
        user.SecurityVersion = model.AccountKeys.SecurityStateData.SecurityVersion;
    }

    public void RotateV1Keys(RotateUserAccountKeysData model, User user)
    {
        // Changing the public key encryption key pair is not supported during key rotation for now; so this ensures it is not accidentally changed
        var providedPublicKey = model.AccountPublicKey;
        if (providedPublicKey != user.PublicKey)
        {
            throw new InvalidOperationException("The provided account public key does not match the user's current public key, and changing the account asymmetric keypair is currently not supported during key rotation.");
        }
        if (GetEncryptionType(model.UserKeyEncryptedAccountPrivateKey) != EncryptionType.AesCbc256_HmacSha256_B64)
        {
            throw new InvalidOperationException("The provided user key encrypted account private key was not wrapped with AES-256-CBC-HMAC");
        }
        // Private key is re-wrapped with new user key by client
        user.PrivateKey = model.UserKeyEncryptedAccountPrivateKey;
    }

    public void UpgradeKeysToV2(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        if (string.IsNullOrEmpty(model.AccountKeys.PublicKeyEncryptionKeyPairData?.SignedPublicKey))
        {
            throw new InvalidOperationException("The provided public key encryption key pair data does not contain a valid signed public key.");
        }
        if (model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey != user.PublicKey)
        {
            throw new InvalidOperationException("The provided public key encryption key pair data does not match the user's current public key.");
        }
        if (GetEncryptionType(model.AccountKeys.SignatureKeyPairData.WrappedSigningKey) != EncryptionType.XChaCha20Poly1305_B64)
        {
            throw new InvalidOperationException("The provided signing key data is not wrapped with XChaCha20-Poly1305.");
        }
        if (GetEncryptionType(model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey) != EncryptionType.XChaCha20Poly1305_B64)
        {
            throw new InvalidOperationException("The provided public key encryption private key data is not wrapped with XChaCha20-Poly1305.");
        }

        saveEncryptedDataActions.Add(_userSignatureKeyPairRepository.SetUserSignatureKeyPair(user.Id, model.AccountKeys.SignatureKeyPairData));
        user.PrivateKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey;
        user.SignedPublicKey = model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey;
        user.SecurityState = model.AccountKeys.SecurityStateData.SecurityState;
        user.SecurityVersion = model.AccountKeys.SecurityStateData.SecurityVersion;
    }

    public async Task UpdateAccountKeys(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        var isV2User = await IsV2EncryptionUserAsync(user);
        var isUpgradingToV2 = model.AccountKeys.SignatureKeyPairData != null && !isV2User;
        if (isV2User)
        {
            await RotateV2Keys(model, user, saveEncryptedDataActions);
        }
        else if (isUpgradingToV2)
        {
            UpgradeKeysToV2(model, user, saveEncryptedDataActions);
        }
        else
        {
            RotateV1Keys(model, user);
        }
    }

    void UpdateUserData(RotateUserAccountKeysData model, User user, List<UpdateEncryptedDataForKeyRotation> saveEncryptedDataActions)
    {
        // RevisionDate is used to detect outdated ciphers that are not synced and up-to-date. Without cipher-key encryption, this is
        // required to detect that ciphers are outdated after a key-rotation on clients that did not receive the logout notification.
        var now = DateTime.UtcNow;

        if (model.Ciphers.Any())
        {
            var ciphersWithUpdatedDate = model.Ciphers.Select(c => { c.RevisionDate = now; return c; }).ToList().AsEnumerable();
            saveEncryptedDataActions.Add(_cipherRepository.UpdateForKeyRotation(user.Id, ciphersWithUpdatedDate));
        }

        if (model.Folders.Any())
        {
            var foldersWithUpdatedDate = model.Folders.Select(f => { f.RevisionDate = now; return f; }).ToList().AsEnumerable();
            saveEncryptedDataActions.Add(_folderRepository.UpdateForKeyRotation(user.Id, foldersWithUpdatedDate));
        }

        if (model.Sends.Any())
        {
            var sendsWithUpdatedDate = model.Sends.Select(s => { s.RevisionDate = now; return s; }).ToList().AsEnumerable();
            saveEncryptedDataActions.Add(_sendRepository.UpdateForKeyRotation(user.Id, sendsWithUpdatedDate));
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

    /// <summary>
    /// Helper method to convert an encryption type string to an enum value.
    /// </summary>
    private EncryptionType GetEncryptionType(string encString)
    {
        var parts = encString.Split('.');
        if (parts.Length == 0)
        {
            throw new ArgumentException("Invalid encryption type string.", nameof(encString));
        }
        if (byte.TryParse(parts[0], out var encryptionTypeNumber))
        {
            return (EncryptionType)encryptionTypeNumber;
        }
        throw new ArgumentException("Invalid encryption type string.", nameof(encString));
    }
}
