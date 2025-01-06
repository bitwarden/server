using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey.Implementations;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserFeatures.UserKey;

[SutProviderCustomize]
public class RotateUserKeyCommandTests
{
    [Theory, BitAutoData]
    public async Task RotateUserKeyAsync_Success(SutProvider<RotateUserKeyCommand> sutProvider, User user,
        RotateUserKeyData model)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.MasterPasswordHash)
            .Returns(true);
        foreach (var webauthnCred in model.WebAuthnKeys)
        {
            var dbWebauthnCred = new WebAuthnCredential
            {
                EncryptedPublicKey = "encryptedPublicKey",
                EncryptedUserKey = "encryptedUserKey"
            };
            sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetByIdAsync(webauthnCred.Id, user.Id)
                .Returns(dbWebauthnCred);
        }

        var result = await sutProvider.Sut.RotateUserKeyAsync(user, model);

        Assert.Equal(IdentityResult.Success, result);
    }

    [Theory, BitAutoData]
    public async Task RotateUserKeyAsync_InvalidMasterPasswordHash_ReturnsFailedIdentityResult(
        SutProvider<RotateUserKeyCommand> sutProvider, User user, RotateUserKeyData model)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.MasterPasswordHash)
            .Returns(false);
        foreach (var webauthnCred in model.WebAuthnKeys)
        {
            var dbWebauthnCred = new WebAuthnCredential
            {
                EncryptedPublicKey = "encryptedPublicKey",
                EncryptedUserKey = "encryptedUserKey"
            };
            sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetByIdAsync(webauthnCred.Id, user.Id)
                .Returns(dbWebauthnCred);
        }

        var result = await sutProvider.Sut.RotateUserKeyAsync(user, model);

        Assert.False(result.Succeeded);
    }

    [Theory, BitAutoData]
    public async Task RotateUserKeyAsync_LogsOutUser(
        SutProvider<RotateUserKeyCommand> sutProvider, User user, RotateUserKeyData model)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(user, model.MasterPasswordHash)
            .Returns(true);
        foreach (var webauthnCred in model.WebAuthnKeys)
        {
            var dbWebauthnCred = new WebAuthnCredential
            {
                EncryptedPublicKey = "encryptedPublicKey",
                EncryptedUserKey = "encryptedUserKey"
            };
            sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetByIdAsync(webauthnCred.Id, user.Id)
                .Returns(dbWebauthnCred);
        }

        await sutProvider.Sut.RotateUserKeyAsync(user, model);

        await sutProvider.GetDependency<IPushNotificationService>().ReceivedWithAnyArgs()
            .PushLogOutAsync(default, default);
    }

}
