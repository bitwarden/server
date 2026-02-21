using Bit.Api.Vault.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Vault.Models.Response;

public class SyncResponseModelTests
{
    private const string _mockEncryptedKey1 = "2.key1==|data1==|hmac1==";
    private const string _mockEncryptedKey2 = "2.key2==|data2==|hmac2==";
    private const string _mockEncryptedKey3 = "2.key3==|data3==|hmac3==";

    private static SyncResponseModel CreateSyncResponseModel(
        User user,
        IEnumerable<WebAuthnCredential>? webAuthnCredentials = null)
    {
        return new SyncResponseModel(
            new GlobalSettings(),
            user,
            new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("private", "public", null)
            },
            false,
            false,
            new Dictionary<Guid, OrganizationAbility>(),
            new List<Guid>(),
            new List<OrganizationUserOrganizationDetails>(),
            new List<ProviderUserProviderDetails>(),
            new List<ProviderUserOrganizationDetails>(),
            new List<Folder>(),
            new List<CollectionDetails>(),
            new List<CipherDetails>(),
            new Dictionary<Guid, IGrouping<Guid, CollectionCipher>>(),
            true, // excludeDomains: true to avoid JSON deserialization issues in tests
            new List<Policy>(),
            new List<Send>(),
            webAuthnCredentials ?? new List<WebAuthnCredential>());
    }

    [Theory]
    [BitAutoData]
    public void Constructor_UserWithMasterPassword_SetsMasterPasswordUnlock(User user)
    {
        // Arrange
        user.MasterPassword = "hashed-password";
        user.Key = _mockEncryptedKey1;
        user.Kdf = KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;

        // Act
        var result = CreateSyncResponseModel(user);

        // Assert
        Assert.NotNull(result.UserDecryption);
        Assert.NotNull(result.UserDecryption.MasterPasswordUnlock);
        Assert.Equal(_mockEncryptedKey1, result.UserDecryption.MasterPasswordUnlock.MasterKeyEncryptedUserKey);
        Assert.Equal(user.Email.ToLowerInvariant(), result.UserDecryption.MasterPasswordUnlock.Salt);
        Assert.NotNull(result.UserDecryption.MasterPasswordUnlock.Kdf);
        Assert.Equal(KdfType.Argon2id, result.UserDecryption.MasterPasswordUnlock.Kdf.KdfType);
        Assert.Equal(3, result.UserDecryption.MasterPasswordUnlock.Kdf.Iterations);
        Assert.Equal(64, result.UserDecryption.MasterPasswordUnlock.Kdf.Memory);
        Assert.Equal(4, result.UserDecryption.MasterPasswordUnlock.Kdf.Parallelism);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_UserWithoutMasterPassword_MasterPasswordUnlockIsNull(User user)
    {
        // Arrange
        user.MasterPassword = null;

        // Act
        var result = CreateSyncResponseModel(user);

        // Assert
        Assert.NotNull(result.UserDecryption);
        Assert.Null(result.UserDecryption.MasterPasswordUnlock);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithEnabledWebAuthnPrfCredentials_SetsWebAuthnPrfOptions(
        User user,
        WebAuthnCredential credential)
    {
        // Arrange
        credential.SupportsPrf = true;
        credential.EncryptedPrivateKey = _mockEncryptedKey1;
        credential.EncryptedUserKey = _mockEncryptedKey2;
        credential.EncryptedPublicKey = _mockEncryptedKey3;

        // Act
        var result = CreateSyncResponseModel(user, new List<WebAuthnCredential> { credential });

        // Assert
        Assert.NotNull(result.UserDecryption);
        Assert.NotNull(result.UserDecryption.WebAuthnPrfOptions);
        Assert.Single(result.UserDecryption.WebAuthnPrfOptions);
        var option = result.UserDecryption.WebAuthnPrfOptions[0];
        Assert.Equal(_mockEncryptedKey1, option.EncryptedPrivateKey);
        Assert.Equal(_mockEncryptedKey2, option.EncryptedUserKey);
        Assert.Equal(credential.CredentialId, option.CredentialId);
        Assert.Empty(option.Transports);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithoutEnabledWebAuthnPrfCredentials_WebAuthnPrfOptionsIsNull(User user)
    {
        // Act
        var result = CreateSyncResponseModel(user);

        // Assert
        Assert.NotNull(result.UserDecryption);
        Assert.Null(result.UserDecryption.WebAuthnPrfOptions);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_UserWithV2UpgradeToken_SetsV2UpgradeToken(User user)
    {
        // Arrange
        var tokenData = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedKey1,
            WrappedUserKey2 = _mockEncryptedKey2
        };
        user.V2UpgradeToken = tokenData.ToJson();

        // Act
        var result = CreateSyncResponseModel(user);

        // Assert
        Assert.NotNull(result.UserDecryption);
        Assert.NotNull(result.UserDecryption.V2UpgradeToken);
        Assert.Equal(_mockEncryptedKey1, result.UserDecryption.V2UpgradeToken.WrappedUserKey1);
        Assert.Equal(_mockEncryptedKey2, result.UserDecryption.V2UpgradeToken.WrappedUserKey2);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_UserWithoutV2UpgradeToken_V2UpgradeTokenIsNull(User user)
    {
        // Arrange
        user.V2UpgradeToken = null;

        // Act
        var result = CreateSyncResponseModel(user);

        // Assert
        Assert.NotNull(result.UserDecryption);
        Assert.Null(result.UserDecryption.V2UpgradeToken);
    }
}
