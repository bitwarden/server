using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.UserKey.Implementations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserKey;

[SutProviderCustomize]
public class RotateUserKeyCommandTests
{
    [Theory, BitAutoData]
    public async Task RotateUserKeyAsync_Success(SutProvider<RotateUserKeyCommand> sutProvider, RotateUserKeyData model)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(model.User, model.MasterPasswordHash)
            .Returns(true);

        var result = await sutProvider.Sut.RotateUserKeyAsync(model);

        Assert.Equal(IdentityResult.Success, result);
    }

    [Theory, BitAutoData]
    public async Task RotateUserKeyAsync_InvalidMasterPasswordHash_ReturnsFailedIdentityResult(
        SutProvider<RotateUserKeyCommand> sutProvider, RotateUserKeyData model)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(model.User, model.MasterPasswordHash)
            .Returns(false);

        var result = await sutProvider.Sut.RotateUserKeyAsync(model);

        Assert.Equal(false, result.Succeeded);
    }

    [Theory, BitAutoData]
    public async Task RotateUserKeyAsync_LogsOutUser(
        SutProvider<RotateUserKeyCommand> sutProvider, RotateUserKeyData model)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(model.User, model.MasterPasswordHash)
            .Returns(true);

        await sutProvider.Sut.RotateUserKeyAsync(model);

        await sutProvider.GetDependency<IPushNotificationService>().ReceivedWithAnyArgs()
            .PushLogOutAsync(default, default);
    }
}
