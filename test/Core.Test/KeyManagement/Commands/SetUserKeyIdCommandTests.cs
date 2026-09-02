using Bit.Core.Entities;
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
    [Theory, BitAutoData]
    public async Task SetUserKeyIdAsync_NoKeyIdRecorded_StoresTheKeyId(
        User user,
        KeyId userKeyId,
        SutProvider<SetUserKeyIdCommand> sutProvider)
    {
        // Arrange - an account that pre-dates the key id
        user.UserKeyId = null;

        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var mockUpdateUserData = Substitute.For<UpdateUserData>();
        userRepository.SetUserKeyId(user.Id, userKeyId).Returns(mockUpdateUserData);

        // Act
        await sutProvider.Sut.SetUserKeyIdAsync(user, userKeyId);

        // Assert
        userRepository.Received(1).SetUserKeyId(user.Id, userKeyId);
        await userRepository
            .Received(1)
            .UpdateUserDataAsync(Arg.Is<IEnumerable<UpdateUserData>>(actions =>
                actions.Count() == 1 && actions.First() == mockUpdateUserData));
    }

    [Theory, BitAutoData]
    public async Task SetUserKeyIdAsync_KeyIdAlreadyRecorded_ThrowsAndWritesNothing(
        User user,
        KeyId userKeyId,
        SutProvider<SetUserKeyIdCommand> sutProvider)
    {
        // Arrange - reporting a key id must not rename a key the account is already known to use
        user.UserKeyId = "fedcba9876543210fedcba9876543210";

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetUserKeyIdAsync(user, userKeyId));

        // Assert
        Assert.Equal("User key id is already set.", exception.Message);

        var userRepository = sutProvider.GetDependency<IUserRepository>();
        userRepository.DidNotReceive().SetUserKeyId(Arg.Any<Guid>(), Arg.Any<KeyId>());
        await userRepository.DidNotReceive().UpdateUserDataAsync(Arg.Any<IEnumerable<UpdateUserData>>());
    }
}
