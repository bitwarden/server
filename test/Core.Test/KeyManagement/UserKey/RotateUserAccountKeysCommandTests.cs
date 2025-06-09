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
    public async Task RejectsWrongOldMasterPassword(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        user.Email = model.MasterPasswordUnlockData.Email;
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(false);

        var result = await sutProvider.Sut.RotateUserAccountKeysAsync(user, model);

        Assert.NotEqual(IdentityResult.Success, result);
    }

    [Theory, BitAutoData]
    public async Task ThrowsWhenUserIsNull(SutProvider<RotateUserAccountKeysCommand> sutProvider,
          RotateUserAccountKeysData model)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(null, model));
    }

    [Theory, BitAutoData]
    public async Task RejectsEmailChange(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        user.PublicKey = "old-public";
        user.PrivateKey = "2.xxx";

        user.Kdf = Enums.KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;

        model.MasterPasswordUnlockData.Email = user.Email + ".different-domain";
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;
        model.UserKeyEncryptedAccountPrivateKey = "2.xxx";

        model.AccountPublicKey = user.PublicKey;
        model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = user.PublicKey;
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
    }

    [Theory, BitAutoData]
    public async Task RejectsKdfChange(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        user.PublicKey = "old-public";
        user.PrivateKey = "2.xxx";

        user.Kdf = Enums.KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;

        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.PBKDF2_SHA256;
        model.MasterPasswordUnlockData.KdfIterations = 600000;
        model.MasterPasswordUnlockData.KdfMemory = null;
        model.MasterPasswordUnlockData.KdfParallelism = null;
        model.UserKeyEncryptedAccountPrivateKey = "2.xxx";
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
    }


    [Theory, BitAutoData]
    public async Task RejectsPublicKeyChange(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        user.PublicKey = "old-public";
        user.PrivateKey = "2.xxx";

        user.Kdf = Enums.KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;

        model.AccountPublicKey = "new-public";
        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
    }

    [Theory, BitAutoData]
    public async Task RotatesCorrectlyV1Async(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        user.Kdf = Enums.KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;
        user.PrivateKey = "2.xxx";
        user.SignedPublicKey = null;

        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;

        model.AccountPublicKey = user.PublicKey;
        model.UserKeyEncryptedAccountPrivateKey = "2.xxx";
        model.AccountKeys.SignatureKeyPairData = null;

        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        var result = await sutProvider.Sut.RotateUserAccountKeysAsync(user, model);

        Assert.Equal(IdentityResult.Success, result);
    }

    [Theory, BitAutoData]
    public async Task ThrowsWhenSignatureKeyPairMissingInModelForV2User(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        // Simulate v2 user (e.g., by setting a property or flag, depending on implementation)
        user.Kdf = Enums.KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;
        user.PublicKey = "public-key";
        user.PrivateKey = "7.xxx";
        // Remove signature key pair
        if (model.AccountKeys != null)
        {
            model.AccountKeys.SignatureKeyPairData = null;
        }
        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;
        model.AccountPublicKey = user.PublicKey;
        model.UserKeyEncryptedAccountPrivateKey = "2.xxx";
        model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = user.PublicKey;
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(user.Id)
            .Returns(new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "dummyWrappedSigningKey", "dummyVerifyingKey"));
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
        Assert.Equal("The provided user key encrypted account private key was not wrapped with XChaCha20-Poly1305", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task DoesNotThrowWhenSignatureKeyPairPresentForV2User(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user,
        RotateUserAccountKeysData model)
    {
        user.Kdf = Enums.KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;
        user.PublicKey = "public-key";
        user.PrivateKey = "2.xxx";
        // Ensure signature key pair is present
        if (model.AccountKeys != null)
        {
            model.AccountKeys.SignatureKeyPairData = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "dummyWrappedSigningKey", "dummyVerifyingKey");
        }
        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;
        model.AccountPublicKey = user.PublicKey;
        model.UserKeyEncryptedAccountPrivateKey = "2.xxx";
        model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = user.PublicKey;
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        var result = await sutProvider.Sut.RotateUserAccountKeysAsync(user, model);
        Assert.Equal(IdentityResult.Success, result);
    }


    [Theory, BitAutoData]
    public async Task UpdateAccountKeys_ThrowsIfPublicKeyChangesAsync(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        user.PrivateKey = "2.xxx";
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>()
            .GetByUserIdAsync(user.Id)
            .ReturnsNull();
        user.PublicKey = "old-public";
        model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = "new-public";
        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeys(model, user, saveEncryptedDataActions));
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeys_ThrowsIfPrivateKeyWrappedWithNotXchacha20ForV2UserAsync(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        user.PrivateKey = "7.xxx";
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>()
            .GetByUserIdAsync(user.Id)
            .Returns(new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "7.xxx", "public"));
        user.PublicKey = "public";
        model.UserKeyEncryptedAccountPrivateKey = "2.xxx";
        model.AccountPublicKey = user.PublicKey;
        model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.xxx", "public", null);
        model.AccountKeys.SignatureKeyPairData = null;
        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeys(model, user, saveEncryptedDataActions));
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeys_ThrowsIfIsV1UserAndPrivateKeyIsWrappedWithNotAesCbcHmacAsync(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        user.PrivateKey = "2.xxx";
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>()
            .GetByUserIdAsync(user.Id)
            .ReturnsNull();
        user.PublicKey = "public";
        model.UserKeyEncryptedAccountPrivateKey = "7.xxx";
        model.AccountPublicKey = "public";
        model.AccountKeys.PublicKeyEncryptionKeyPairData = null;
        model.AccountKeys.SignatureKeyPairData = null;
        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.UpdateAccountKeys(model, user, saveEncryptedDataActions));
    }

    [Theory, BitAutoData]
    public async Task UpdateAccountKeys_SuccessAsync(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        user.PrivateKey = "2.xxx";
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>()
            .GetByUserIdAsync(user.Id)
            .Returns(new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "7.xxx", "public"));
        user.PublicKey = "public";
        model.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey = "public";
        model.UserKeyEncryptedAccountPrivateKey = "2.xxx";
        var saveEncryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        await sutProvider.Sut.UpdateAccountKeys(model, user, saveEncryptedDataActions);
        Assert.NotEmpty(saveEncryptedDataActions);
    }


    [Theory, BitAutoData]
    public void ValidateRotationModelSignatureKeyPairForv1UserAndUpgradeToV2_NoSignedPublicKeyThrows(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        model.AccountKeys.SignatureKeyPairData = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "signingKey", "verifyingKey");
        model.AccountKeys.PublicKeyEncryptionKeyPairData.SignedPublicKey = null;
        var encryptedDataActions = new List<Core.KeyManagement.UserKey.UpdateEncryptedDataForKeyRotation>();
        var exception = Assert.Throws<InvalidOperationException>(() => sutProvider.Sut.ValidateRotationModelSignatureKeyPairForV1UserAndUpgradeToV2(model, user, encryptedDataActions));
        Assert.Equal("The provided public key encryption key pair data does not contain a valid signed public key.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ThrowsIfSignatureKeyPairDataIsNull(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        user.Kdf = Enums.KdfType.Argon2id;
        user.PrivateKey = "2.abc";
        user.PublicKey = "public-key";
        if (model.AccountKeys != null)
        {
            model.AccountKeys.SignatureKeyPairData = null;
        }
        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;
        model.AccountPublicKey = user.PublicKey;
        model.UserKeyEncryptedAccountPrivateKey = "2.abc";
        model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.abc", user.PublicKey, "signed-public-key");
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(user.Id)
            .Returns(new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "dummyWrappedSigningKey", "dummyVerifyingKey"));
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
        Assert.Equal("The provided signature key pair data is missing.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ThrowsIfVerifyingKeyDoesNotMatch(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        user.Kdf = Enums.KdfType.Argon2id;
        user.PrivateKey = "2.abc";
        user.PublicKey = "public-key";
        var repoKeyPair = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "signingKey", "verifyingKey");
        var modelKeyPair = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "signingKey", "verifyingKey-different");
        if (model.AccountKeys == null)
        {
            model.AccountKeys = new Core.KeyManagement.Models.Data.Models.UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.abc", user.PublicKey, "signed-public-key"),
                SignatureKeyPairData = modelKeyPair
            };
        }
        else
        {
            model.AccountKeys.SignatureKeyPairData = modelKeyPair;
            model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.abc", user.PublicKey, "signed-public-key");
        }
        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;
        model.AccountPublicKey = user.PublicKey;
        model.UserKeyEncryptedAccountPrivateKey = "2.abc";
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(user.Id)
            .Returns(repoKeyPair);
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
        Assert.Equal("The provided verifying key does not match the expected value.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ThrowsIfSignedPublicKeyIsNullOrEmpty(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        user.Kdf = Enums.KdfType.Argon2id;
        user.PrivateKey = "2.abc";
        user.PublicKey = "public-key";
        var keyPair = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "dummyWrappedSigningKey", "dummyVerifyingKey");
        if (model.AccountKeys == null)
        {
            model.AccountKeys = new Core.KeyManagement.Models.Data.Models.UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.abc", user.PublicKey, null),
                SignatureKeyPairData = keyPair
            };
        }
        else
        {
            model.AccountKeys.SignatureKeyPairData = keyPair;
            model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.abc", user.PublicKey, null);
        }
        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;
        model.AccountPublicKey = user.PublicKey;
        model.UserKeyEncryptedAccountPrivateKey = "2.abc";
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(user.Id)
            .Returns(keyPair);
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
        Assert.Equal("The provided public key encryption key pair data does not contain a valid signed public key.", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ThrowsIfWrappedSigningKeyIsNotXChaCha20(SutProvider<RotateUserAccountKeysCommand> sutProvider, User user, RotateUserAccountKeysData model)
    {
        user.Kdf = Enums.KdfType.Argon2id;
        user.PrivateKey = "2.abc";
        user.PublicKey = "public-key";
        var keyPair = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "signingKey", "verifyingKey");
        if (model.AccountKeys == null)
        {
            model.AccountKeys = new Core.KeyManagement.Models.Data.Models.UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.abc", user.PublicKey, "signed-public-key"),
                SignatureKeyPairData = keyPair
            };
        }
        else
        {
            model.AccountKeys.SignatureKeyPairData = keyPair;
            model.AccountKeys.PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.abc", user.PublicKey, "signed-public-key");
        }
        model.MasterPasswordUnlockData.Email = user.Email;
        model.MasterPasswordUnlockData.KdfType = Enums.KdfType.Argon2id;
        model.MasterPasswordUnlockData.KdfIterations = 3;
        model.MasterPasswordUnlockData.KdfMemory = 64;
        model.MasterPasswordUnlockData.KdfParallelism = 4;
        model.AccountPublicKey = user.PublicKey;
        model.UserKeyEncryptedAccountPrivateKey = "2.abc";
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(user.Id)
            .Returns(keyPair);
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.OldMasterKeyAuthenticationHash)
            .Returns(true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sutProvider.Sut.RotateUserAccountKeysAsync(user, model));
        Assert.Equal("The provided wrapped signing key is not XChaCha20-encrypted.", ex.Message);
    }
}
