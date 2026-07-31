using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

[SutProviderCustomize]
public class SetUserKeyIdCommandTests
{
    private static readonly KeyId _userKeyId =
        KeyId.FromHexEncodedString("0123456789abcdef0123456789abcdef");

    [Theory, BitAutoData]
    public async Task SetUserKeyIdAsync_WhenNoKeyIdStored_StoresIt(
        Guid userId,
        SutProvider<SetUserKeyIdCommand> sutProvider)
    {
        sutProvider.GetDependency<IUserRepository>()
            .TrySetUserKeyIdAsync(userId, _userKeyId)
            .Returns(true);

        await sutProvider.Sut.SetUserKeyIdAsync(userId, _userKeyId);

        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .TrySetUserKeyIdAsync(userId, _userKeyId);
    }

    [Theory, BitAutoData]
    public async Task SetUserKeyIdAsync_WhenKeyIdAlreadyStored_ThrowsBadRequest(
        Guid userId,
        SutProvider<SetUserKeyIdCommand> sutProvider)
    {
        sutProvider.GetDependency<IUserRepository>()
            .TrySetUserKeyIdAsync(userId, _userKeyId)
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetUserKeyIdAsync(userId, _userKeyId));

        Assert.Equal("User key id is already set.", exception.Message);
    }
}
