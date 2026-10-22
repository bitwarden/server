using System.Security.Claims;
using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;
using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using IFeatureService = Bitwarden.Server.Sdk.Features.IFeatureService;

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(SsoJitProvisionFinishController))]
[SutProviderCustomize]
public class SsoJitProvisionFinishControllerPasswordTests
{
    private const string _orgIdentifier = "org-sso-identifier";

    [Theory]
    [BitAutoData]
    public async Task FinishMasterPasswordAsync_UserNull_ThrowsUnauthorizedAccessException(
        SutProvider<SsoJitProvisionFinishController> sutProvider, User user)
    {
        // Arrange
        var model = CreateV2Model(user, acceptOrganizationInvite: false);
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.FinishMasterPasswordAsync(model));
        await AssertNoCommandCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async Task FinishMasterPasswordAsync_V2_PassesAcceptOrganizationInviteToCommand(
        bool acceptOrganizationInvite,
        SutProvider<SsoJitProvisionFinishController> sutProvider, User user)
    {
        // Arrange
        var model = CreateV2Model(user, acceptOrganizationInvite);
        ArrangeUser(sutProvider, user);
        ArrangeV2Flag(sutProvider, enabled: true);

        // Act
        await sutProvider.Sut.FinishMasterPasswordAsync(model);

        // Assert
        await sutProvider.GetDependency<IFinishSsoJitProvisionMasterPasswordCommand>().Received(1)
            .FinishProvisionAsync(
                user,
                Arg.Is<SetInitialMasterPasswordDataModel>(d =>
                    d.MasterPasswordAuthentication != null &&
                    d.MasterPasswordUnlock != null &&
                    d.AccountKeys != null &&
                    d.OrgSsoIdentifier == _orgIdentifier),
                acceptOrganizationInvite);
        await sutProvider.GetDependency<ISetInitialMasterPasswordCommandV1>().DidNotReceiveWithAnyArgs()
            .SetInitialMasterPasswordAsync(default!, default!, default!, default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task FinishMasterPasswordAsync_V2FlagOff_WithAccountKeys_ThrowsBadRequestException(
        SutProvider<SsoJitProvisionFinishController> sutProvider, User user)
    {
        // Arrange
        var model = CreateV2Model(user, acceptOrganizationInvite: false);
        ArrangeUser(sutProvider, user);
        ArrangeV2Flag(sutProvider, enabled: false);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.FinishMasterPasswordAsync(model));

        // Assert
        Assert.Equal("Request includes V2 AccountKeys but V2 encryption is not enabled.", exception.Message);
        await AssertNoCommandCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(false, false)]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    [BitAutoData(true, true)]
    public async Task FinishMasterPasswordAsync_V1_PassesAcceptOrganizationInviteToV1Command(
        bool acceptOrganizationInvite, bool v2FlagEnabled,
        SutProvider<SsoJitProvisionFinishController> sutProvider, User user)
    {
        // Arrange - V1 request (MPAD + MPUD + legacy Keys) routes to V1 regardless of the V2 flag.
        var model = CreateV1Model(user, acceptOrganizationInvite);
        ArrangeUser(sutProvider, user);
        ArrangeV2Flag(sutProvider, v2FlagEnabled);
        ArrangeV1CommandResult(sutProvider, IdentityResult.Success);

        // Act
        await sutProvider.Sut.FinishMasterPasswordAsync(model);

        // Assert
        await sutProvider.GetDependency<ISetInitialMasterPasswordCommandV1>().Received(1)
            .SetInitialMasterPasswordAsync(
                user,
                model.MasterPasswordAuthentication!.MasterPasswordAuthenticationHash,
                model.MasterPasswordUnlock!.MasterKeyWrappedUserKey,
                _orgIdentifier,
                acceptOrganizationInvite);
        await sutProvider.GetDependency<IFinishSsoJitProvisionMasterPasswordCommand>().DidNotReceiveWithAnyArgs()
            .FinishProvisionAsync(default!, default!, default);

        Assert.Equal("newPublicKey", user.PublicKey);
        Assert.Equal("newEncryptedPrivateKey", user.PrivateKey);
        Assert.Equal(model.MasterPasswordUnlock.Salt, user.MasterPasswordSalt);
    }

    [Theory]
    [BitAutoData]
    public async Task FinishMasterPasswordAsync_V1_CommandFails_ThrowsBadRequestException(
        SutProvider<SsoJitProvisionFinishController> sutProvider, User user)
    {
        // Arrange
        var model = CreateV1Model(user, acceptOrganizationInvite: false);
        ArrangeUser(sutProvider, user);
        ArrangeV1CommandResult(sutProvider, IdentityResult.Failed(new IdentityError { Description = "failed" }));

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.FinishMasterPasswordAsync(model));
    }

    [Theory]
    [BitAutoData]
    public async Task FinishMasterPasswordAsync_V1_SaltDiffers_ThrowsBadRequestException(
        SutProvider<SsoJitProvisionFinishController> sutProvider, User user)
    {
        // Arrange
        var model = CreateV1Model(user, acceptOrganizationInvite: false, salt: "different-salt");
        ArrangeUser(sutProvider, user);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.FinishMasterPasswordAsync(model));
        await AssertNoCommandCalledAsync(sutProvider);
    }

    private static void ArrangeUser(SutProvider<SsoJitProvisionFinishController> sutProvider, User user)
    {
        user.Key = null;
        user.MasterPassword = null;
        user.PublicKey = null;
        user.PrivateKey = null;
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);
    }

    private static void ArrangeV2Flag(SutProvider<SsoJitProvisionFinishController> sutProvider, bool enabled)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.EnableAccountEncryptionV2JitPasswordRegistration)
            .Returns(enabled);
    }

    private static void ArrangeV1CommandResult(SutProvider<SsoJitProvisionFinishController> sutProvider,
        IdentityResult result)
    {
        sutProvider.GetDependency<ISetInitialMasterPasswordCommandV1>()
            .SetInitialMasterPasswordAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>())
            .Returns(result);
    }

    private static async Task AssertNoCommandCalledAsync(SutProvider<SsoJitProvisionFinishController> sutProvider)
    {
        await sutProvider.GetDependency<IFinishSsoJitProvisionMasterPasswordCommand>().DidNotReceiveWithAnyArgs()
            .FinishProvisionAsync(default!, default!, default);
        await sutProvider.GetDependency<ISetInitialMasterPasswordCommandV1>().DidNotReceiveWithAnyArgs()
            .SetInitialMasterPasswordAsync(default!, default!, default!, default!, default);
    }

    private static SsoJitProvisionFinishSetInitialPasswordRequestModel CreateV2Model(User user,
        bool acceptOrganizationInvite)
    {
        var model = CreateModernModel(user.GetMasterPasswordSalt(), acceptOrganizationInvite);
        model.AccountKeys = new AccountKeysRequestModel
        {
            UserKeyEncryptedAccountPrivateKey = "privateKey",
            AccountPublicKey = "publicKey"
        };
        return model;
    }

    private static SsoJitProvisionFinishSetInitialPasswordRequestModel CreateV1Model(User user,
        bool acceptOrganizationInvite, string? salt = null)
    {
        var model = CreateModernModel(salt ?? user.GetMasterPasswordSalt(), acceptOrganizationInvite);
#pragma warning disable CS0618 // V1 MP JIT clients still send the legacy keypair
        model.Keys = new KeysRequestModel
        {
            PublicKey = "newPublicKey",
            EncryptedPrivateKey = "newEncryptedPrivateKey"
        };
#pragma warning restore CS0618
        return model;
    }

    private static SsoJitProvisionFinishSetInitialPasswordRequestModel CreateModernModel(string salt,
        bool acceptOrganizationInvite)
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };
        return new SsoJitProvisionFinishSetInitialPasswordRequestModel
        {
            OrgIdentifier = _orgIdentifier,
            AcceptOrganizationInvite = acceptOrganizationInvite,
            MasterPasswordHint = "hint",
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = salt
            }
        };
    }
}
