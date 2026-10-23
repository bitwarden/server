using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;
using Bit.Api.Models.Request;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.UserFeatures.DeviceTrust;
using Bit.Core.Auth.UserFeatures.DeviceTrust.Data;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(SsoJitProvisionFinishController))]
[SutProviderCustomize]
public class SsoJitProvisionFinishControllerTrustedDeviceTests
{
    private const string _userKeyId = "0123456789abcdef0123456789abcdef";
    private const string _deviceIdentifier = "device-identifier";

    [Theory, BitAutoData]
    public async Task FinishTrustedDeviceAsync_UserNotFound_ThrowsUnauthorizedAccessException(
        SutProvider<SsoJitProvisionFinishController> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default!).ReturnsNullForAnyArgs();
        var model = CreateModel(organizationId, acceptOrganizationInvite: true, includeDeviceKeys: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.FinishTrustedDeviceAsync(model));

        await sutProvider.GetDependency<IFinishSsoJitProvisionTrustedDeviceCommand>().DidNotReceiveWithAnyArgs()
            .FinishProvisionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(true, true)]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    public async Task FinishTrustedDeviceAsync_MapsRequestToCommand(
        bool acceptOrganizationInvite, bool includeDeviceKeys,
        SutProvider<SsoJitProvisionFinishController> sutProvider, User user, Guid organizationId)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default!).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(_deviceIdentifier);
        var model = CreateModel(organizationId, acceptOrganizationInvite, includeDeviceKeys);

        await sutProvider.Sut.FinishTrustedDeviceAsync(model);

        await sutProvider.GetDependency<IFinishSsoJitProvisionTrustedDeviceCommand>().Received(1)
            .FinishProvisionAsync(
                user,
                Arg.Is<FinishSsoJitProvisionTrustedDeviceData>(data =>
                    data.OrganizationId == organizationId &&
                    data.ResetPasswordKey == model.ResetPasswordKey &&
                    data.AccountKeys.IsV2Encryption() &&
                    data.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey == model.Keys.AccountKeys.AccountPublicKey &&
                    data.UserKeyId != null &&
                    (includeDeviceKeys
                        ? data.DeviceKeys != null &&
                          data.DeviceKeys.DeviceIdentifier == _deviceIdentifier &&
                          data.DeviceKeys.EncryptedUserKey == model.DeviceKeys!.EncryptedUserKey &&
                          data.DeviceKeys.EncryptedPublicKey == model.DeviceKeys.EncryptedPublicKey &&
                          data.DeviceKeys.EncryptedPrivateKey == model.DeviceKeys.EncryptedPrivateKey
                        : data.DeviceKeys == null)),
                acceptOrganizationInvite);
    }

    [Theory, BitAutoData]
    public async Task FinishTrustedDeviceAsync_DeviceKeysWithoutCurrentDevice_ThrowsBadRequest(
        SutProvider<SsoJitProvisionFinishController> sutProvider, User user, Guid organizationId)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default!).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.ReturnsNull();
        var model = CreateModel(organizationId, acceptOrganizationInvite: true, includeDeviceKeys: true);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.FinishTrustedDeviceAsync(model));

        await sutProvider.GetDependency<IFinishSsoJitProvisionTrustedDeviceCommand>().DidNotReceiveWithAnyArgs()
            .FinishProvisionAsync(default!, default!, default);
    }

    private static SsoJitProvisionFinishTrustedDeviceRequestModel CreateModel(
        Guid organizationId, bool acceptOrganizationInvite, bool includeDeviceKeys)
    {
        return new SsoJitProvisionFinishTrustedDeviceRequestModel
        {
            Keys = new KeysRequestModel
            {
#pragma warning disable CS0618 // Type or member is obsolete
                PublicKey = "public-key",
                EncryptedPrivateKey = "7.wrapped-private-key",
#pragma warning restore CS0618
                AccountKeys = new AccountKeysRequestModel
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
                },
                UserKeyId = _userKeyId
            },
            OrganizationId = organizationId,
            ResetPasswordKey = "4.YWJjZA==",
            DeviceKeys = includeDeviceKeys
                ? new DeviceKeysRequestModel
                {
                    EncryptedUserKey = "4.device-user-key",
                    EncryptedPublicKey = "2.device|public|key",
                    EncryptedPrivateKey = "2.device|private|key"
                }
                : null,
            AcceptOrganizationInvite = acceptOrganizationInvite
        };
    }
}
