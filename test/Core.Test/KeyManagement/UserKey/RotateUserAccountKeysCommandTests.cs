using Bit.Core.Entities;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.KeyManagement.UserKey.Implementations;
using Bit.Core.Services;
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
    [Theory, BitAutoData]
    public async Task RotateUserAccountKeysAsync_WrongOldMasterPassword_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        user.Email = model.MasterPasswordUnlockData.Email;
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(false);

        var result = await sutProvider.Sut.RotateUserAccountKeysAsync(user, model);

        Assert.NotEqual(IdentityResult.Success, result);
    }

    [Theory, BitAutoData]
    public async Task RotateUserAccountKeysAsync_UserIsNull_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider,
          RotateUserAccountKeysData model)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(null, model));
    }

    [Theory, BitAutoData]
    public async Task RotateUserAccountKeysAsync_EmailChange_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);

        model.MasterPasswordUnlockData.Email = user.Email + ".different-domain";
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
    }

    [Theory, BitAutoData]
    public async Task RotateUserAccountKeysAsync_KdfChange_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);

        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.PBKDF2_SHA256;
        model.MasterPasswordUnlockData.KdfIterations = 600000;
        model.MasterPasswordUnlockData.KdfMemory = null;
        model.MasterPasswordUnlockData.KdfParallelism = null;
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
    }


    [Theory, BitAutoData]
    public async Task RotateUserAccountKeysAsync_PublicKeyChange_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);

        model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = "new-public";
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
    }

    [Theory, BitAutoData]
    public async Task RotateUserAccountKeysAsync_V1_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        var result = await sutProvider.Sut.RotateUserAccountKeysAsync(user, model);
        Assert.Equal(IdentityResult.Success, result);
    }

    [Theory, BitAutoData]
    public async Task RotateUserAccountKeysAsync_UpgradeV1ToV2_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        var result = await sutProvider.Sut.RotateUserAccountKeysAsync(user, model);
        Assert.Equal(IdentityResult.Success, result);
    }


    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_PublicKeyChange_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);

        model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = "new-public";
        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2User_PrivateKeyNotXChaCha20_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = "2.xxx";

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V1User_PrivateKeyNotAesCbcHmac_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = "7.xxx";

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided account private key was not wrapped with AES-256-CBC-HMAC", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V1_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions);
        Assert.Empty(saveEncryptedDataActions);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2_Success(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions);
        Assert.NotEmpty(saveEncryptedDataActions);
    }



    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2User_VerifyingKeyMismatch_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SignatureKeyPairData.VerifyingKey = "different-verifying-key";

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided verifying key does not match the user's current verifying key.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2User_SignedPublicKeyNullOrEmpty_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey = null;

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("No signed public key provided, but the user already has a signature key pair.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_V2User_WrappedSigningKeyNotXChaCha20_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SignatureKeyPairData.WrappedSigningKey = "2.xxx";

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided signing key data is not wrapped with XChaCha20-Poly1305.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateEncryptedDataForKeyRotation_UpgradeToV2_InvalidVerifyingKey_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SignatureKeyPairData.VerifyingKey = "";

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided signature key pair data does not contain a valid verifying key.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateEncryptedDataForKeyRotation_UpgradeToV2_IncorrectlyWrappedPrivateKey_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = "2.abc";

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("The provided private key encryption key is not wrapped with XChaCha20-Poly1305.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateEncryptedDataForKeyRotation_UpgradeToV2_NoSignedPublicKey_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey = null;

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("No signed public key provided, but the user already has a signature key pair.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateEncryptedDataForKeyRotation_RotateV2_NoSignatureKeyPair_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV2ExistingUser(user, signatureRepository);
        SetV2ModelUser(model);
        model.AccountKeys.SignatureKeyPairData = null;

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("Signature key pair data is required for V2 encryption.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_GetEncryptionType_EmptyString_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = "";

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("Invalid encryption type string.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeysAsync_GetEncryptionType_InvalidString_Rejects(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        SetTestKdfAndSaltForUserAndModel(user, model);
        var signatureRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        SetV1ExistingUser(user, signatureRepository);
        SetV1ModelUser(model);
        model.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey = "9.xxx";

        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await sutProvider.Sut.UpdateAccountKeysAsync(model, user, saveEncryptedDataActions));
        Assert.Equal("Invalid encryption type string.", ex.Message);
    }

    // Helper functions to set valid test parameters that match each other to the model and user. 
    private static void SetTestKdfAndSaltForUserAndModel(User user, RotateUserAccountKeysData model)
    {
        user.Kdf = Enums.KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;
        // The email is the salt for the KDF and is validated currently.
        user.Email = model.MasterPasswordUnlockData.Email;
    }

    private static void SetV1ExistingUser(User user, IUserSignatureKeyPairRepository userSignatureKeyPairRepository)
    {
        user.PrivateKey = "2.abc";
        user.PublicKey = "public";
        user.SignedPublicKey = null;
        userSignatureKeyPairRepository.GetByUserIdAsync(user.Id).ReturnsNull();
    }

    private static void SetV2ExistingUser(User user, IUserSignatureKeyPairRepository userSignatureKeyPairRepository)
    {
        user.PrivateKey = "7.abc";
        user.PublicKey = "public";
        user.SignedPublicKey = "signed-public";
        userSignatureKeyPairRepository.GetByUserIdAsync(user.Id).Returns(new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "7.abc", "verifying-key"));
    }

    private static void SetV1ModelUser(RotateUserAccountKeysData model)
    {
        model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.abc", "public", null);
        model.AccountKeys.SignatureKeyPairData = null;
    }

    private static void SetV2ModelUser(RotateUserAccountKeysData model)
    {
        model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("7.abc", "public", "signed-public");
        model.AccountKeys.SignatureKeyPairData = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "7.abc", "verifying-key");
    }
}
