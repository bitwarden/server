using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;
using Bit.Api.Models.Request;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request.SsoJitProvisionFinish;

public class SsoJitProvisionFinishTrustedDeviceRequestModelTests
{
    private const string _resetPasswordKey = "4.YWJjZA==";
    private const string _userKeyId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Validate_V2AccountKeys_NoErrors()
    {
        var model = CreateModel(CreateV2AccountKeys());

        Assert.Empty(model.Validate(new ValidationContext(model)));
    }

    [Fact]
    public void Validate_LegacyKeypairOnly_NoErrors()
    {
        var model = CreateModel(accountKeys: null);

        Assert.Empty(model.Validate(new ValidationContext(model)));
    }

    [Fact]
    public void Validate_V1AccountKeys_ReturnsError()
    {
        var model = CreateModel(new AccountKeysRequestModel
        {
            AccountPublicKey = "public-key",
            UserKeyEncryptedAccountPrivateKey = "2.private-key"
        });

        var result = Assert.Single(model.Validate(new ValidationContext(model)));
        Assert.Equal("AccountKeys are only supported for V2 encryption.", result.ErrorMessage);
        Assert.Contains(nameof(SsoJitProvisionFinishTrustedDeviceRequestModel.Keys), result.MemberNames);
    }

    [Fact]
    public void Validate_PartialV2AccountKeys_ReturnsError()
    {
        var accountKeys = CreateV2AccountKeys();
        accountKeys.SignatureKeyPair = null;
        var model = CreateModel(accountKeys);

        var result = Assert.Single(model.Validate(new ValidationContext(model)));
        Assert.Equal("AccountKeys are only supported for V2 encryption.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmptyOrganizationId_ReturnsError()
    {
        var model = CreateModel(CreateV2AccountKeys());
        model.OrganizationId = Guid.Empty;

        var result = Assert.Single(model.Validate(new ValidationContext(model)));
        Assert.Contains(nameof(SsoJitProvisionFinishTrustedDeviceRequestModel.OrganizationId), result.MemberNames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_AcceptInviteWithoutResetPasswordKey_ReturnsError(string? resetPasswordKey)
    {
        var model = CreateModel(CreateV2AccountKeys());
        model.ResetPasswordKey = resetPasswordKey;

        var result = Assert.Single(model.Validate(new ValidationContext(model)));
        Assert.Contains(nameof(SsoJitProvisionFinishTrustedDeviceRequestModel.ResetPasswordKey), result.MemberNames);
    }

    [Fact]
    public void Validate_OpenInviteWithoutResetPasswordKey_NoErrors()
    {
        var model = CreateModel(CreateV2AccountKeys());
        model.ResetPasswordKey = null;
        model.AcceptOrganizationInvite = false;

        Assert.Empty(model.Validate(new ValidationContext(model)));
    }

    [Fact]
    public void ToData_V2AccountKeys_MapsAccountKeysAndUserKeyId()
    {
        var model = CreateModel(CreateV2AccountKeys());

        var data = model.ToData(currentDeviceIdentifier: null);

        Assert.True(data.AccountKeys.IsV2Encryption());
        Assert.Equal("public-key", data.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey);
        Assert.NotNull(data.UserKeyId);
        Assert.Equal(model.OrganizationId, data.OrganizationId);
        Assert.Equal(_resetPasswordKey, data.ResetPasswordKey);
        Assert.Null(data.DeviceKeys);
    }

    [Fact]
    public void ToData_LegacyKeypair_MapsToV1AccountKeysWithoutUserKeyId()
    {
        var model = CreateModel(accountKeys: null);

        var data = model.ToData(currentDeviceIdentifier: null);

        Assert.False(data.AccountKeys.IsV2Encryption());
        Assert.Equal("legacy-public-key", data.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey);
        Assert.Equal("2.legacy-private-key", data.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey);
        Assert.Null(data.UserKeyId);
    }

    [Fact]
    public void ToData_DeviceKeys_UsesCurrentDeviceIdentifier()
    {
        var model = CreateModel(CreateV2AccountKeys());
        model.DeviceKeys = CreateDeviceKeys();

        var data = model.ToData("current-device");

        Assert.NotNull(data.DeviceKeys);
        Assert.Equal("current-device", data.DeviceKeys.DeviceIdentifier);
        Assert.Equal(model.DeviceKeys.EncryptedUserKey, data.DeviceKeys.EncryptedUserKey);
        Assert.Equal(model.DeviceKeys.EncryptedPublicKey, data.DeviceKeys.EncryptedPublicKey);
        Assert.Equal(model.DeviceKeys.EncryptedPrivateKey, data.DeviceKeys.EncryptedPrivateKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ToData_DeviceKeysWithoutCurrentDeviceIdentifier_ThrowsBadRequest(string? currentDeviceIdentifier)
    {
        var model = CreateModel(CreateV2AccountKeys());
        model.DeviceKeys = CreateDeviceKeys();

        Assert.Throws<BadRequestException>(() => model.ToData(currentDeviceIdentifier));
    }

    private static SsoJitProvisionFinishTrustedDeviceRequestModel CreateModel(AccountKeysRequestModel? accountKeys)
    {
        return new SsoJitProvisionFinishTrustedDeviceRequestModel
        {
            Keys = new KeysRequestModel
            {
#pragma warning disable CS0618 // Type or member is obsolete
                PublicKey = "legacy-public-key",
                EncryptedPrivateKey = "2.legacy-private-key",
#pragma warning restore CS0618
                AccountKeys = accountKeys,
                UserKeyId = _userKeyId
            },
            OrganizationId = Guid.NewGuid(),
            ResetPasswordKey = _resetPasswordKey,
            AcceptOrganizationInvite = true
        };
    }

    private static DeviceKeysRequestModel CreateDeviceKeys()
    {
        return new DeviceKeysRequestModel
        {
            EncryptedUserKey = "4.device-user-key",
            EncryptedPublicKey = "2.device|public|key",
            EncryptedPrivateKey = "2.device|private|key"
        };
    }

    private static AccountKeysRequestModel CreateV2AccountKeys()
    {
        return new AccountKeysRequestModel
        {
            AccountPublicKey = "public-key",
            UserKeyEncryptedAccountPrivateKey = "7.wrapped-private-key",
            PublicKeyEncryptionKeyPair = new PublicKeyEncryptionKeyPairRequestModel
            {
                PublicKey = "public-key",
                WrappedPrivateKey = "7.wrapped-private-key",
                SignedPublicKey = "signed-public-key"
            },
            SignatureKeyPair = new SignatureKeyPairRequestModel
            {
                SignatureAlgorithm = "ed25519",
                WrappedSigningKey = "7.wrapped-signing-key",
                VerifyingKey = "verifying-key"
            },
            SecurityState = new SecurityStateModel { SecurityState = "v2", SecurityVersion = 2 }
        };
    }
}
