using System.Security.Claims;
using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(SsoJitProvisionFinishController))]
[SutProviderCustomize]
public class SsoJitProvisionFinishControllerKeyConnectorTests
{
    private const string _orgIdentifier = "test-org";
    private const string _containedKeyId = "0123456789abcdef0123456789abcdef";

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task FinishKeyConnectorAsync_UserNull_ThrowsUnauthorized(
        bool isV2Request,
        SutProvider<SsoJitProvisionFinishController> sutProvider)
    {
        // Arrange
        var model = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = isV2Request ? CreateV2Keys() : CreateV1Keys(),
            AcceptOrganizationInvite = true
        };
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.FinishKeyConnectorAsync(model));

        await sutProvider.GetDependency<ISetKeyConnectorKeyCommand>().DidNotReceiveWithAnyArgs()
            .SetKeyConnectorKeyForUserAsync(default!, default!, default);
        await sutProvider.GetDependency<IUserService>().DidNotReceiveWithAnyArgs()
            .SetKeyConnectorKeyAsync(default!, default!, default!, default);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task FinishKeyConnectorAsync_V2Request_CallsSetKeyConnectorKeyCommandWithFlag(
        bool acceptOrganizationInvite,
        SutProvider<SsoJitProvisionFinishController> sutProvider,
        User user)
    {
        // Arrange
        var keys = CreateV2Keys();
        var model = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = keys,
            AcceptOrganizationInvite = acceptOrganizationInvite
        };
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);

        // Act
        await sutProvider.Sut.FinishKeyConnectorAsync(model);

        // Assert
        await sutProvider.GetDependency<ISetKeyConnectorKeyCommand>().Received(1).SetKeyConnectorKeyForUserAsync(
            user,
            Arg.Is<KeyConnectorKeysData>(data =>
                data.KeyConnectorKeyWrappedUserKey == keys.KeyConnectorKeyWrappedUserKey &&
                data.AccountKeys == keys.AccountKeys &&
                data.OrgIdentifier == _orgIdentifier &&
                data.ContainedKeyId != null &&
                data.ContainedKeyId.ToString() == _containedKeyId),
            acceptOrganizationInvite);
        await sutProvider.GetDependency<IUserService>().DidNotReceiveWithAnyArgs()
            .SetKeyConnectorKeyAsync(default!, default!, default!, default);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task FinishKeyConnectorAsync_V1Request_CallsUserServiceWithToUserValuesAndFlag(
        bool acceptOrganizationInvite,
        SutProvider<SsoJitProvisionFinishController> sutProvider,
        User user)
    {
        // Arrange
        user.PublicKey = null;
        user.PrivateKey = null;
        var keys = CreateV1Keys();
        var model = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = keys,
            AcceptOrganizationInvite = acceptOrganizationInvite
        };
        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        userService.SetKeyConnectorKeyAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(IdentityResult.Success);

        // Act
        await sutProvider.Sut.FinishKeyConnectorAsync(model);

        // Assert
        await userService.Received(1).SetKeyConnectorKeyAsync(
            Arg.Is<User>(u =>
                u.Id == user.Id &&
                u.Key == keys.Key &&
                u.Kdf == keys.Kdf &&
                u.KdfIterations == keys.KdfIterations &&
                u.KdfMemory == keys.KdfMemory &&
                u.KdfParallelism == keys.KdfParallelism &&
                u.PublicKey == keys.Keys!.PublicKey &&
                u.PrivateKey == keys.Keys.EncryptedPrivateKey),
            keys.Key!,
            _orgIdentifier,
            acceptOrganizationInvite);
        await sutProvider.GetDependency<ISetKeyConnectorKeyCommand>().DidNotReceiveWithAnyArgs()
            .SetKeyConnectorKeyForUserAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task FinishKeyConnectorAsync_V1RequestFails_ThrowsBadRequestWithErrors(
        SutProvider<SsoJitProvisionFinishController> sutProvider,
        User user)
    {
        // Arrange
        user.PublicKey = null;
        user.PrivateKey = null;
        var model = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = CreateV1Keys(),
            AcceptOrganizationInvite = false
        };
        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        userService.SetKeyConnectorKeyAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "set key connector key failed" }));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.FinishKeyConnectorAsync(model));

        Assert.Equal(1, exception.ModelState!.ErrorCount);
        Assert.Equal("set key connector key failed", exception.ModelState.Root.Errors[0].ErrorMessage);
    }

    private static SetKeyConnectorKeyRequestModel CreateV2Keys() =>
        new()
        {
            KeyConnectorKeyWrappedUserKey = "wrapped-user-key",
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = "public-key",
                UserKeyEncryptedAccountPrivateKey = "encrypted-private-key"
            },
            ContainedKeyId = _containedKeyId,
            OrgIdentifier = _orgIdentifier
        };

    private static SetKeyConnectorKeyRequestModel CreateV1Keys() =>
        new()
        {
            Key = "key-connector-wrapped-user-key",
            Keys = new KeysRequestModel { PublicKey = "public-key", EncryptedPrivateKey = "encrypted-private-key" },
            Kdf = KdfType.Argon2id,
            KdfIterations = 3,
            KdfMemory = 64,
            KdfParallelism = 4,
            OrgIdentifier = _orgIdentifier
        };
}
