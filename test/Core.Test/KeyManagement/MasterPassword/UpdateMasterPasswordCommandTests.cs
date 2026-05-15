#nullable enable

using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.MasterPassword;
using Bit.Core.KeyManagement.MasterPassword.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.MasterPassword;

[SutProviderCustomize]
public class UpdateMasterPasswordCommandTests
{
    private static UpdateMasterPasswordData CreateData(string salt) =>
        new()
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
                MasterPasswordAuthenticationHash = "client-auth-hash",
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
                MasterKeyWrappedUserKey = "new-master-key-wrapped-user-key",
                Salt = salt
            }
        };

    [Theory]
    [BitAutoData]
    public async Task RunAsync_DelegatesToQueryThenPersists(
        SutProvider<UpdateMasterPasswordCommand> sutProvider, User user)
    {
        var data = CreateData(user.Email);

        await sutProvider.Sut.RunAsync(user, data);

        await sutProvider.GetDependency<IUpdateMasterPasswordQuery>().Received(1).RunAsync(user, data);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_QueryThrows_DoesNotCallRepository(
        SutProvider<UpdateMasterPasswordCommand> sutProvider, User user)
    {
        var data = CreateData(user.Email);
        sutProvider.GetDependency<IUpdateMasterPasswordQuery>()
            .RunAsync(Arg.Any<User>(), Arg.Any<UpdateMasterPasswordData>())
            .Returns(Task.FromException(new InvalidOperationException("Validation failed.")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(user, data));

        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_PersistsUserReturnedByQuery(
        SutProvider<UpdateMasterPasswordCommand> sutProvider, User user)
    {
        var data = CreateData(user.Email);

        await sutProvider.Sut.RunAsync(user, data);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Is<User>(u => u.Id == user.Id));
    }
}
