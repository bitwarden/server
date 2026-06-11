using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.KeyManagement.UserKey.Implementations;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey;

[SutProviderCustomize]
public class RotateUserAccountKeysCommandTests
{
    private static readonly string _mockEncryptedType2String =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private static readonly string _mockEncryptedType2String2 =
        "2.06CDSJjTZaigYHUuswIq5A==|trxgZl2RCkYrrmCvGE9WNA==|w5p05eI5wsaYeSyWtsAPvBX63vj798kIMxBTfSB0BQg=";
    private static readonly string _mockEncryptedType7String = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private static readonly string _mockEncryptedType7String2 = "7.Mi1iaXR3YXJkZW4tZGF0YQo=";
    private static readonly string _mockSalt = "salt@example.com";

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_WrongOldMasterPassword_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        PasswordChangeAndRotateUserAccountKeysData model)
    {
        user.Email = model.MasterPasswordUnlockData.Salt;
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(false);

        var result = await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model);

        Assert.NotEqual(IdentityResult.Success, result);
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_UserIsNull_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider,
        PasswordChangeAndRotateUserAccountKeysData model)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(null, model));
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_EmailChange_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        PasswordChangeAndRotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);

        model.MasterPasswordUnlockData = new MasterPasswordUnlockData
        {
            Kdf = model.MasterPasswordUnlockData.Kdf,
            Salt = user.Email + ".different-domain",
            MasterKeyWrappedUserKey = model.MasterPasswordUnlockData.MasterKeyWrappedUserKey
        };

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model));
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_KdfChange_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        PasswordChangeAndRotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);

        model.MasterPasswordUnlockData = new MasterPasswordUnlockData
        {
            Kdf = new KdfSettings
            {
                KdfType = KdfType.PBKDF2_SHA256,
                Iterations = 600000,
                Memory = null,
                Parallelism = null
            },
            Salt = model.MasterPasswordUnlockData.Salt,
            MasterKeyWrappedUserKey = model.MasterPasswordUnlockData.MasterKeyWrappedUserKey
        };

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model));
    }


    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_PublicKeyChange_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        PasswordChangeAndRotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);

        model.BaseData.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = "new-public";
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model));
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_V1_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        PasswordChangeAndRotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        var result = await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model);
        Assert.Equal(IdentityResult.Success, result);
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_UpgradeV1ToV2_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        PasswordChangeAndRotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model.BaseData);

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        var result = await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model);
        Assert.Equal(IdentityResult.Success, result);
        Assert.Equal(user.SecurityState, model.BaseData.AccountKeys.SecurityStateData!.SecurityState);
    }


    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_PublicKeyChange_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);

        model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = "new-public";
        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2User_PrivateKeyNotXChaCha20_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = _mockEncryptedType2String;

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V1User_PrivateKeyNotAesCbcHmac_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = _mockEncryptedType7String;

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided account private key was not wrapped with AES-256-CBC-HMAC", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V1_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions);
        Assert.Empty(saveEncryptedDataActions);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions);
        Assert.NotEmpty(saveEncryptedDataActions);
        Assert.Equal(user.SecurityState, model.AccountKeys.SecurityStateData!.SecurityState);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2User_VerifyingKeyMismatch_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SignatureKeyPairData.VerifyingKey = "different-verifying-key";

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided verifying key does not match the user's current verifying key.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2User_SignedPublicKeyNullOrEmpty_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey = null;

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("No signed public key provided, but the user already has a signature key pair.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2User_WrappedSigningKeyNotXChaCha20_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SignatureKeyPairData.WrappedSigningKey = _mockEncryptedType2String;

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided signing key data is not wrapped with XChaCha20-Poly1305.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeys_UpgradeToV2_InvalidVerifyingKey_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SignatureKeyPairData.VerifyingKey = "";

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided signature key pair data does not contain a valid verifying key.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_UpgradeToV2_IncorrectlyWrappedPrivateKey_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = _mockEncryptedType2String;

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided private key encryption key is not wrapped with XChaCha20-Poly1305.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_UpgradeToV2_NoSignedPublicKey_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey = null;

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("No signed public key provided, but the user already has a signature key pair.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_UpgradeToV2_NoSecurityState_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SecurityStateData = null;

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("No signed security state provider for V2 user", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_RotateV2_NoSignatureKeyPair_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SignatureKeyPairData = null;

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("Signature key pair data is required for V2 encryption.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_GetEncryptionType_EmptyString_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = "";

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("Invalid encryption type string.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_GetEncryptionType_InvalidString_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = "9.xxx";

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("Invalid encryption type string.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserData_RevisionDateChanged_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, BaseRotateUserAccountKeysData model)
    {
        var oldDate = new DateTime(2017, 1, 1);

        var cipher = Substitute.For<Cipher>();
        cipher.RevisionDate = oldDate;
        model.Ciphers = [cipher];

        var folder = Substitute.For<Folder>();
        folder.RevisionDate = oldDate;
        model.Folders = [folder];

        var send = Substitute.For<Send>();
        send.RevisionDate = oldDate;
        model.Sends = [send];

        var saveEncryptedDataActions = new List<UpdateEncryptedDataForKeyRotation>();

        sutProvider.Sut.UpdateUserData(model, user, saveEncryptedDataActions);
        foreach (var dataAction in saveEncryptedDataActions)
        {
            await dataAction.Invoke();
        }

        var updatedCiphers = sutProvider.GetDependency<ICipherRepository>()
            .ReceivedCalls()
            .FirstOrDefault(call => call.GetMethodInfo().Name == "UpdateForKeyRotation")?
            .GetArguments()[1] as IEnumerable<Cipher>;
        foreach (var updatedCipher in updatedCiphers!)
        {
            var oldCipher = model.Ciphers.FirstOrDefault(c => c.Id == updatedCipher.Id);
            Assert.NotEqual(oldDate, updatedCipher.RevisionDate);
        }

        var updatedFolders = sutProvider.GetDependency<IFolderRepository>()
            .ReceivedCalls()
            .FirstOrDefault(call => call.GetMethodInfo().Name == "UpdateForKeyRotation")?
            .GetArguments()[1] as IEnumerable<Folder>;
        foreach (var updatedFolder in updatedFolders!)
        {
            var oldFolder = model.Folders.FirstOrDefault(f => f.Id == updatedFolder.Id);
            Assert.NotEqual(oldDate, updatedFolder.RevisionDate);
        }

        var updatedSends = sutProvider.GetDependency<ISendRepository>()
            .ReceivedCalls()
            .FirstOrDefault(call => call.GetMethodInfo().Name == "UpdateForKeyRotation")?
            .GetArguments()[1] as IEnumerable<Send>;
        foreach (var updatedSend in updatedSends!)
        {
            var oldSend = model.Sends.FirstOrDefault(s => s.Id == updatedSend.Id);
            Assert.NotEqual(oldDate, updatedSend.RevisionDate);
        }
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_WithV2UpgradeToken_NoLogout(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, PasswordChangeAndRotateUserAccountKeysData model)
    {
        // Arrange
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);

        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        // Act
        await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model);

        // Assert - Security stamp is not updated
        Assert.Equal(originalSecurityStamp, user.SecurityStamp);

        // Assert - Token is stored on user
        Assert.NotNull(user.V2UpgradeToken);
        Assert.Contains(_mockEncryptedType7String, user.V2UpgradeToken);
        Assert.Contains(_mockEncryptedType2String, user.V2UpgradeToken);

        // Assert - Push notification sent with KeyRotation reason
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id, false, Enums.PushNotificationLogOutReason.KeyRotation);
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_WithoutV2UpgradeToken_Logout(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, PasswordChangeAndRotateUserAccountKeysData model)
    {
        // Arrange
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);

        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        user.V2UpgradeToken = null;
        model.BaseData.V2UpgradeToken = null;

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        // Act
        await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model);

        // Assert - Security stamp is updated
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);

        // Assert - Token is not stored on user
        Assert.Null(user.V2UpgradeToken);

        // Assert - Push notification sent without reason
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_WithExistingToken_WithoutNewToken_ClearsStaleToken(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, PasswordChangeAndRotateUserAccountKeysData model)
    {
        // Arrange
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);

        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();

        // User has existing stale token from previous rotation
        var staleToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };
        user.V2UpgradeToken = staleToken.ToJson();

        // Model does NOT provide new token
        model.BaseData.V2UpgradeToken = null;

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        // Act
        await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model);

        // Assert - Stale token explicitly cleared
        Assert.Null(user.V2UpgradeToken);

        // Assert - Security stamp is updated (logout behavior)
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);

        // Assert - Push notification sent without reason (standard logout)
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_WithExistingToken_WithNewToken_UpdatesToken(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, PasswordChangeAndRotateUserAccountKeysData model)
    {
        // Arrange
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);

        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();

        // User has existing token from previous rotation
        var oldToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };
        user.V2UpgradeToken = oldToken.ToJson();

        // Model provides NEW token
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String2,
            WrappedUserKey2 = _mockEncryptedType2String2
        };

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        // Act
        await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model);

        // Assert - Security stamp is not updated (no logout)
        Assert.Equal(originalSecurityStamp, user.SecurityStamp);

        // Assert - Token contains new wrapped keys
        Assert.NotNull(user.V2UpgradeToken);
        Assert.Contains(_mockEncryptedType7String2, user.V2UpgradeToken);
        Assert.Contains(_mockEncryptedType2String2, user.V2UpgradeToken);

        // Assert - Token does NOT contain old wrapped keys
        Assert.DoesNotContain(oldToken.WrappedUserKey1, user.V2UpgradeToken);
        Assert.DoesNotContain(oldToken.WrappedUserKey2, user.V2UpgradeToken);

        // Assert - Push notification sent with KeyRotation reason (no logout)
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id, false, Enums.PushNotificationLogOutReason.KeyRotation);
    }

    [Theory, BitAutoData]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync_V2User_WithV2UpgradeToken_IgnoresTokenAndLogsOut(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, PasswordChangeAndRotateUserAccountKeysData model)
    {
        // Arrange
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model.BaseData);

        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        // Act
        await sutProvider.Sut.PasswordChangeAndRotateUserAccountKeysAsync(user, model);

        // Assert - Token is NOT stored (V2 users don't need upgrade token)
        Assert.Null(user.V2UpgradeToken);

        // Assert - Security stamp IS updated (full logout)
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);

        // Assert - Standard logout push, not KeyRotation reason
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task MasterPasswordRotateUserAccountKeysAsync_MissingUser_Throws(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, MasterPasswordRotateUserAccountKeysData model) =>
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await sutProvider.Sut.MasterPasswordRotateUserAccountKeysAsync(null, model));

    [Theory]
    [BitAutoData(true, true)]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    public async Task MasterPasswordRotateUserAccountKeysAsync_UserIsNotMasterPasswordUser_Throws(bool keyNull,
        bool masterPasswordNull,
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, MasterPasswordRotateUserAccountKeysData model)
    {
        if (keyNull)
        {
            user.Key = null;
        }

        if (masterPasswordNull)
        {
            user.MasterPassword = null;
        }

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.MasterPasswordRotateUserAccountKeysAsync(user, model));
    }

    [Theory]
    [BitAutoData]
    public async Task MasterPasswordRotateUserAccountKeysAsync_EmailChange_Throws(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, MasterPasswordRotateUserAccountKeysData model)
    {
        model = SetupTestData(model);
        SetupUserKdf(user, model);
        user.Email += ".different-domain";

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.MasterPasswordRotateUserAccountKeysAsync(user, model));
    }

    [Theory]
    [BitAutoData]
    public async Task MasterPasswordRotateUserAccountKeysAsync_ChangedKdf_Throws(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, MasterPasswordRotateUserAccountKeysData model)
    {
        model = SetupTestData(model);
        SetupUserKdf(user, model);
        user.Kdf = KdfType.PBKDF2_SHA256;

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await sutProvider.Sut.MasterPasswordRotateUserAccountKeysAsync(user, model));
    }

    [Theory]
    [BitAutoData]
    public async Task MasterPasswordRotateUserAccountKeysAsync_V2User_Success(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, MasterPasswordRotateUserAccountKeysData model)
    {
        model = SetupTestData(model);
        SetupUserKdf(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();

        await sutProvider.Sut.MasterPasswordRotateUserAccountKeysAsync(user, model);

        Assert.Equal(model.MasterPasswordUnlockData.MasterKeyWrappedUserKey, user.Key);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .UpdateUserKeyAndEncryptedDataV2Async(user, Arg.Any<IEnumerable<UpdateEncryptedDataForKeyRotation>>());
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task MasterPasswordRotateUserAccountKeysAsync_V1User_WithNewV2UpgradeToken_PersistsToken(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, MasterPasswordRotateUserAccountKeysData model)
    {
        model = SetupTestData(model);
        SetupUserKdf(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        await sutProvider.Sut.MasterPasswordRotateUserAccountKeysAsync(user, model);

        Assert.NotNull(user.V2UpgradeToken);
        Assert.Contains(_mockEncryptedType7String, user.V2UpgradeToken);
        Assert.Contains(_mockEncryptedType2String, user.V2UpgradeToken);
        Assert.Equal(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id, false, PushNotificationLogOutReason.KeyRotation);
    }

    [Theory]
    [BitAutoData]
    public async Task MasterPasswordRotateUserAccountKeysAsync_V2User_WithV2UpgradeToken_IgnoresTokenAndLogsOut(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, MasterPasswordRotateUserAccountKeysData model)
    {
        model = SetupTestData(model);
        SetupUserKdf(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        await sutProvider.Sut.MasterPasswordRotateUserAccountKeysAsync(user, model);

        Assert.Null(user.V2UpgradeToken);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task TdeRotateUserAccountKeysAsync_UserIsNull_ThrowsArgumentNullException(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, TdeRotateUserAccountKeysData model) =>
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await sutProvider.Sut.TdeRotateUserAccountKeysAsync(null, model));

    [Theory]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, false, true)]
    public async Task TdeRotateUserAccountKeysAsync_UserIsNotTdeUser_ThrowsBadRequestException(
        bool hasKey, bool hasMasterPassword, bool usesKeyConnector,
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, TdeRotateUserAccountKeysData model)
    {
        user.Key = hasKey ? _mockEncryptedType2String : null;
        user.MasterPassword = hasMasterPassword ? "hashedPassword" : null;
        user.UsesKeyConnector = usesKeyConnector;

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.TdeRotateUserAccountKeysAsync(user, model));
    }

    [Theory]
    [BitAutoData]
    public async Task TdeRotateUserAccountKeysAsync_V1User_Success(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, TdeRotateUserAccountKeysData model)
    {
        SetupTdeUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);
        model.BaseData.V2UpgradeToken = null;
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();

        await sutProvider.Sut.TdeRotateUserAccountKeysAsync(user, model);

        Assert.Null(user.Key);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .UpdateUserKeyAndEncryptedDataV2Async(user, Arg.Any<IEnumerable<UpdateEncryptedDataForKeyRotation>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task TdeRotateUserAccountKeysAsync_V2User_Success(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, TdeRotateUserAccountKeysData model)
    {
        SetupTdeUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();

        await sutProvider.Sut.TdeRotateUserAccountKeysAsync(user, model);

        Assert.Null(user.Key);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .UpdateUserKeyAndEncryptedDataV2Async(user, Arg.Any<IEnumerable<UpdateEncryptedDataForKeyRotation>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task TdeRotateUserAccountKeysAsync_V1User_WithV2UpgradeToken_PersistsToken(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, TdeRotateUserAccountKeysData model)
    {
        SetupTdeUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        await sutProvider.Sut.TdeRotateUserAccountKeysAsync(user, model);

        Assert.NotNull(user.V2UpgradeToken);
        Assert.Null(user.Key);
        Assert.Contains(_mockEncryptedType7String, user.V2UpgradeToken);
        Assert.Contains(_mockEncryptedType2String, user.V2UpgradeToken);
        Assert.Equal(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id, false, PushNotificationLogOutReason.KeyRotation);
    }

    [Theory]
    [BitAutoData]
    public async Task TdeRotateUserAccountKeysAsync_WithoutV2UpgradeToken_FullLogout(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, TdeRotateUserAccountKeysData model)
    {
        SetupTdeUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = null;

        await sutProvider.Sut.TdeRotateUserAccountKeysAsync(user, model);

        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        Assert.Null(user.V2UpgradeToken);
        Assert.Null(user.Key);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task TdeRotateUserAccountKeysAsync_V2User_WithV2UpgradeToken_IgnoresTokenAndLogsOut(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, TdeRotateUserAccountKeysData model)
    {
        SetupTdeUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        await sutProvider.Sut.TdeRotateUserAccountKeysAsync(user, model);

        Assert.Null(user.V2UpgradeToken);
        Assert.Null(user.Key);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task KeyConnectorRotateUserAccountKeysAsync_UserIsNull_ThrowsArgumentNullException(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, KeyConnectorRotateUserAccountKeysData model)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await sutProvider.Sut.KeyConnectorRotateUserAccountKeysAsync(null, model));
    }

    [Theory]
    [BitAutoData([null, null, false])]
    [BitAutoData("encrypted-key", "hashedPassword", true)]
    [BitAutoData("encrypted-key", null, false)]
    [BitAutoData([null, null, true])]
    public async Task KeyConnectorRotateUserAccountKeysAsync_UserIsNotKeyConnectorUser_ThrowsBadRequestException(
        string? key, string? masterPassword, bool usesKeyConnector,
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, KeyConnectorRotateUserAccountKeysData model)
    {
        user.Key = key;
        user.MasterPassword = masterPassword;
        user.UsesKeyConnector = usesKeyConnector;

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.KeyConnectorRotateUserAccountKeysAsync(user, model));
    }

    [Theory]
    [BitAutoData]
    public async Task KeyConnectorRotateUserAccountKeysAsync_V1User_Success(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, KeyConnectorRotateUserAccountKeysData model)
    {
        SetupKeyConnectorUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);
        model.BaseData.V2UpgradeToken = null;
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();

        await sutProvider.Sut.KeyConnectorRotateUserAccountKeysAsync(user, model);

        Assert.Equal(model.KeyConnectorKeyWrappedUserKey, user.Key);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .UpdateUserKeyAndEncryptedDataV2Async(user, Arg.Any<IEnumerable<UpdateEncryptedDataForKeyRotation>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task KeyConnectorRotateUserAccountKeysAsync_V2User_Success(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, KeyConnectorRotateUserAccountKeysData model)
    {
        SetupKeyConnectorUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();

        await sutProvider.Sut.KeyConnectorRotateUserAccountKeysAsync(user, model);

        Assert.Equal(model.KeyConnectorKeyWrappedUserKey, user.Key);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .UpdateUserKeyAndEncryptedDataV2Async(user, Arg.Any<IEnumerable<UpdateEncryptedDataForKeyRotation>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task KeyConnectorRotateUserAccountKeysAsync_V1User_WithV2UpgradeToken_PersistsToken(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, KeyConnectorRotateUserAccountKeysData model)
    {
        SetupKeyConnectorUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        await sutProvider.Sut.KeyConnectorRotateUserAccountKeysAsync(user, model);

        Assert.NotNull(user.V2UpgradeToken);
        Assert.Equal(model.KeyConnectorKeyWrappedUserKey, user.Key);
        Assert.Contains(_mockEncryptedType7String, user.V2UpgradeToken);
        Assert.Contains(_mockEncryptedType2String, user.V2UpgradeToken);
        Assert.Equal(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id, false, PushNotificationLogOutReason.KeyRotation);
    }

    [Theory]
    [BitAutoData]
    public async Task KeyConnectorRotateUserAccountKeysAsync_V2User_WithV2UpgradeToken_IgnoresTokenAndLogsOut(
        SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, KeyConnectorRotateUserAccountKeysData model)
    {
        SetupKeyConnectorUser(user);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model.BaseData);
        var originalSecurityStamp = user.SecurityStamp = Guid.NewGuid().ToString();
        model.BaseData.V2UpgradeToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedType7String,
            WrappedUserKey2 = _mockEncryptedType2String
        };

        await sutProvider.Sut.KeyConnectorRotateUserAccountKeysAsync(user, model);

        Assert.Null(user.V2UpgradeToken);
        Assert.Equal(model.KeyConnectorKeyWrappedUserKey, user.Key);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    // Helper functions to set valid test parameters that match each other to the model and user.
    private static void SetTestKdfAndSaltForUserAndModel(User user, PasswordChangeAndRotateUserAccountKeysData model)
    {
        var testKdf = new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4,
        };
        model.MasterPasswordUnlockData = new MasterPasswordUnlockData
        {
            Salt = _mockSalt,
            Kdf = testKdf,
            MasterKeyWrappedUserKey = _mockEncryptedType2String,
        };
        model.MasterPasswordAuthenticationData = new MasterPasswordAuthenticationData
        {
            Salt = _mockSalt,
            Kdf = testKdf,
            MasterPasswordAuthenticationHash = _mockEncryptedType2String,
        };

        user.Kdf = testKdf.KdfType;
        user.KdfIterations = testKdf.Iterations;
        user.KdfMemory = testKdf.Memory;
        user.KdfParallelism = testKdf.Parallelism;

        // The email is the salt for the KDF and is validated currently.
        user.Email = model.MasterPasswordUnlockData.Salt;
        user.MasterPasswordSalt = null;
    }

    private static void SetV1ExistingUser(User user, IUserSignatureKeyPairRepository userSignatureKeyPairRepository)
    {
        user.PrivateKey = _mockEncryptedType2String;
        user.PublicKey = "public";
        user.SignedPublicKey = null;
        userSignatureKeyPairRepository.GetByUserIdAsync(user.Id).ReturnsNull();
    }

    private static void SetV2ExistingUser(User user, IUserSignatureKeyPairRepository userSignatureKeyPairRepository)
    {
        user.PrivateKey = _mockEncryptedType7String;
        user.PublicKey = "public";
        user.SignedPublicKey = "signed-public";
        userSignatureKeyPairRepository.GetByUserIdAsync(user.Id).Returns(new SignatureKeyPairData(SignatureAlgorithm.Ed25519, _mockEncryptedType7String, "verifying-key"));
    }

    private static void SetV1ModelUser(BaseRotateUserAccountKeysData model)
    {
        model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(_mockEncryptedType2String, "public", null);
        model.AccountKeys.SignatureKeyPairData = null;
        model.AccountKeys.SecurityStateData = null;
    }

    private static void SetV2ModelUser(BaseRotateUserAccountKeysData model)
    {
        model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(_mockEncryptedType7String, "public", "signed-public");
        model.AccountKeys.SignatureKeyPairData = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, _mockEncryptedType7String, "verifying-key");
        model.AccountKeys.SecurityStateData = new SecurityStateData
        {
            SecurityState = "abc",
            SecurityVersion = 2,
        };
    }

    private static MasterPasswordRotateUserAccountKeysData SetupTestData(MasterPasswordRotateUserAccountKeysData model)
    {
        var testKdf = new KdfSettings { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 };
        model = new MasterPasswordRotateUserAccountKeysData
        {
            MasterPasswordUnlockData = new MasterPasswordUnlockData
            {
                Kdf = testKdf,
                MasterKeyWrappedUserKey = _mockEncryptedType2String,
                Salt = _mockSalt
            },
            BaseData = model.BaseData
        };
        return model;
    }

    private static void SetupUserKdf(User user, MasterPasswordRotateUserAccountKeysData model)
    {
        user.Kdf = model.MasterPasswordUnlockData.Kdf.KdfType;
        user.KdfIterations = model.MasterPasswordUnlockData.Kdf.Iterations;
        user.KdfMemory = model.MasterPasswordUnlockData.Kdf.Memory;
        user.KdfParallelism = model.MasterPasswordUnlockData.Kdf.Parallelism;
        // For now email and salt are coupled. This will be changed later to read from user.Salt.
        user.Email = model.MasterPasswordUnlockData.Salt;
        user.MasterPasswordSalt = null;
        user.Key = _mockEncryptedType2String;
        user.MasterPassword = "mockMasterPasswordAuthenticationHash";
    }

    private static void SetupTdeUser(User user)
    {
        user.Key = null;
        user.MasterPassword = null;
        user.UsesKeyConnector = false;
    }

    private static void SetupKeyConnectorUser(User user)
    {
        user.Key = _mockEncryptedType2String;
        user.MasterPassword = null;
        user.UsesKeyConnector = true;
    }
}
