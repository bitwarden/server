using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

[SutProviderCustomize]
public class SetKeyConnectorKeyCommandTests
{

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_Success_SetsAccountKeys(
        User user,
        SetKeyConnectorKeyRequestModel requestModel,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        var originalRevisionDate = user.RevisionDate;
        var originalAccountRevisionDate = user.AccountRevisionDate;
        var expectedTime = DateTime.UtcNow;

        // Act
        await sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, requestModel);

        // Assert
        Assert.Equal(requestModel.KeyConnectorKeyWrappedUserKey, user.Key);
        Assert.True(user.UsesKeyConnector);
        Assert.Equal(KdfType.Argon2id, user.Kdf);
        Assert.Equal(3, user.KdfIterations);
        Assert.Equal(64, user.KdfMemory);
        Assert.Equal(4, user.KdfParallelism);
        Assert.NotEqual(originalRevisionDate, user.RevisionDate);
        Assert.NotEqual(originalAccountRevisionDate, user.AccountRevisionDate);
        Assert.Equal(expectedTime, user.RevisionDate, precision: TimeSpan.FromMinutes(1));
        Assert.Equal(expectedTime, user.AccountRevisionDate, precision: TimeSpan.FromMinutes(1));

        sutProvider.GetDependency<ICanUseKeyConnectorQuery>()
            .Received(1)
            .VerifyCanUseKeyConnector(user);

        await sutProvider.GetDependency<ISetAccountKeysForUserCommand>()
            .Received(1)
            .SetAccountKeysForUserAsync(user, requestModel.AccountKeys);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .Received(1)
            .AcceptOrgUserByOrgSsoIdAsync(requestModel.OrgIdentifier, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_NullKeyConnectorKeyWrappedUserKey_ThrowsBadRequestException(
        User user,
        SetKeyConnectorKeyRequestModel requestModel,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        requestModel.KeyConnectorKeyWrappedUserKey = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, requestModel));

        Assert.Equal("KeyConnectorKeyWrappedUserKey and AccountKeys must be provided", exception.Message);

        await sutProvider.GetDependency<ISetAccountKeysForUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .SetAccountKeysForUserAsync(Arg.Any<User>(), Arg.Any<AccountKeysRequestModel>());

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());

        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgSsoIdAsync(Arg.Any<string>(), Arg.Any<User>(), Arg.Any<IUserService>());
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_EmptyKeyConnectorKeyWrappedUserKey_ThrowsBadRequestException(
        User user,
        SetKeyConnectorKeyRequestModel requestModel,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        requestModel.KeyConnectorKeyWrappedUserKey = string.Empty;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, requestModel));

        Assert.Equal("KeyConnectorKeyWrappedUserKey and AccountKeys must be provided", exception.Message);

        await sutProvider.GetDependency<ISetAccountKeysForUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .SetAccountKeysForUserAsync(Arg.Any<User>(), Arg.Any<AccountKeysRequestModel>());

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());

        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgSsoIdAsync(Arg.Any<string>(), Arg.Any<User>(), Arg.Any<IUserService>());
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_NullAccountKeys_ThrowsBadRequestException(
        User user,
        SetKeyConnectorKeyRequestModel requestModel,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        requestModel.AccountKeys = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, requestModel));

        Assert.Equal("KeyConnectorKeyWrappedUserKey and AccountKeys must be provided", exception.Message);

        await sutProvider.GetDependency<ISetAccountKeysForUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .SetAccountKeysForUserAsync(Arg.Any<User>(), Arg.Any<AccountKeysRequestModel>());

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());

        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgSsoIdAsync(Arg.Any<string>(), Arg.Any<User>(), Arg.Any<IUserService>());
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_UserCannotUseKeyConnector_ThrowsException(
        User user,
        SetKeyConnectorKeyRequestModel requestModel,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        var expectedException = new BadRequestException("User cannot use Key Connector");
        sutProvider.GetDependency<ICanUseKeyConnectorQuery>()
            .When(x => x.VerifyCanUseKeyConnector(user))
            .Throw(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, requestModel));

        Assert.Equal(expectedException.Message, exception.Message);

        await sutProvider.GetDependency<ISetAccountKeysForUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .SetAccountKeysForUserAsync(Arg.Any<User>(), Arg.Any<AccountKeysRequestModel>());

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());

        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgSsoIdAsync(Arg.Any<string>(), Arg.Any<User>(), Arg.Any<IUserService>());
    }
}
