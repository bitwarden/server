using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request.SsoJitProvisionFinish;

#pragma warning disable CS0618 // exercising the legacy Keys while they still exist
public class SsoJitProvisionFinishPasswordRequestModelTests
{
    private const string _salt = "user@bitwarden.com";
    private const string _keypairError = "Must specify exactly one of AccountKeys or Keys.";

    [Fact]
    public void Validate_WithAccountKeys_ReturnsNoErrors()
    {
        // Arrange
        var model = CreateModel();
        model.AccountKeys = CreateAccountKeys();

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_WithLegacyKeys_ReturnsNoErrors()
    {
        // Arrange
        var model = CreateModel();
        model.Keys = CreateLegacyKeys();

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_WithoutKeypair_ReturnsKeypairError()
    {
        // Arrange
        var model = CreateModel();

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        var error = Assert.Single(result);
        Assert.Equal(_keypairError, error.ErrorMessage);
        Assert.Equal([nameof(model.AccountKeys), nameof(model.Keys)], error.MemberNames);
    }

    [Fact]
    public void Validate_WithAccountKeysAndLegacyKeys_ReturnsKeypairError()
    {
        // Arrange
        var model = CreateModel();
        model.AccountKeys = CreateAccountKeys();
        model.Keys = CreateLegacyKeys();

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        var error = Assert.Single(result);
        Assert.Equal(_keypairError, error.ErrorMessage);
    }

    [Fact]
    public void Validate_WithMismatchedKdfSettings_ReturnsValidationError()
    {
        // Arrange
        var model = CreateModel(unlockIterations: 650000);
        model.AccountKeys = CreateAccountKeys();

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void Validate_WithMismatchedSalt_ReturnsValidationError()
    {
        // Arrange
        var model = CreateModel(unlockSalt: "different-salt");
        model.AccountKeys = CreateAccountKeys();

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Single(result);
    }

    private static SsoJitProvisionFinishPasswordRequestModel CreateModel(
        int unlockIterations = 600000, string unlockSalt = _salt)
    {
        return new SsoJitProvisionFinishPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            AcceptOrganizationInvite = false,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = _salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = unlockIterations },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = unlockSalt
            }
        };
    }

    private static AccountKeysRequestModel CreateAccountKeys()
    {
        return new AccountKeysRequestModel
        {
            UserKeyEncryptedAccountPrivateKey = "privateKey",
            AccountPublicKey = "publicKey"
        };
    }

    private static KeysRequestModel CreateLegacyKeys()
    {
        return new KeysRequestModel
        {
            PublicKey = "publicKey",
            EncryptedPrivateKey = "encryptedPrivateKey"
        };
    }
}
#pragma warning restore CS0618
