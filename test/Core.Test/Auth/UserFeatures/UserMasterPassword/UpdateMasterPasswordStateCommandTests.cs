using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class UpdateMasterPasswordStateCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task ExecuteAsync_CallsReplaceAsync(
        SutProvider<UpdateMasterPasswordStateCommand> sutProvider,
        User user)
    {
        await sutProvider.Sut.ExecuteAsync(user);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }
}
