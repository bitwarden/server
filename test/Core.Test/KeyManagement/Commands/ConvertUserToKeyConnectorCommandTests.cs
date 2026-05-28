using System.Security.Claims;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

[SutProviderCustomize]
public class ConvertUserToKeyConnectorCommandTests
{
    [Theory]
    [BitAutoData("wrapped-user-key")]
    [BitAutoData("2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=")]
    public async Task ConvertAsync_WrappedUserKeyProvided_SetsWrappedUserKey(
        string wrappedUserKey,
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        // Arrange
        user.UsesKeyConnector = false;
        user.Key = "old-key";
        ArrangeAuthorizationSucceeds(sutProvider, user);

        // Act
        await sutProvider.Sut.ConvertAsync(user, wrappedUserKey);

        // Assert
        Assert.True(user.UsesKeyConnector);
        Assert.Equal(wrappedUserKey, user.Key);
        sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareClearMasterPassword(user);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .ReplaceAsync(Arg.Is<User>(u =>
                u == user &&
                u.Key == wrappedUserKey &&
                u.UsesKeyConnector));
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);
    }

    [Theory]
    [BitAutoData((string?)null)]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task ConvertAsync_WrappedUserKeyNullOrEmptyOrWhitespace_DoesNotOverwriteExistingKey(
        string? wrappedUserKey,
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        // Arrange
        const string existingUserKey = "existing-user-key";
        user.UsesKeyConnector = false;
        user.Key = existingUserKey;
        ArrangeAuthorizationSucceeds(sutProvider, user);

        // Act
        await sutProvider.Sut.ConvertAsync(user, wrappedUserKey);

        // Assert
        Assert.True(user.UsesKeyConnector);
        Assert.Equal(existingUserKey, user.Key);
        sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareClearMasterPassword(user);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .ReplaceAsync(Arg.Is<User>(u =>
                u == user &&
                u.Key == existingUserKey &&
                u.UsesKeyConnector));
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);
    }

    [Theory, BitAutoData]
    public async Task ConvertAsync_NullUser_Throws(
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sutProvider.Sut.ConvertAsync(null!, null));

        sutProvider.GetDependency<IMasterPasswordService>().DidNotReceiveWithAnyArgs()
            .PrepareClearMasterPassword(Arg.Any<User>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());
    }

    [Theory, BitAutoData]
    public async Task ConvertAsync_UserCantUseKeyConnector_ThrowsBadRequest(
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        // Arrange
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(new ClaimsPrincipal());
        sutProvider.GetDependency<ICurrentContext>().HttpContext.Returns(httpContext);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), user, Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Failed());

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConvertAsync(user, null));

        sutProvider.GetDependency<IMasterPasswordService>().DidNotReceiveWithAnyArgs()
            .PrepareClearMasterPassword(Arg.Any<User>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());
    }

    private static void ArrangeAuthorizationSucceeds(
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(new ClaimsPrincipal());
        sutProvider.GetDependency<ICurrentContext>().HttpContext.Returns(httpContext);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), user, Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Success());
    }
}
