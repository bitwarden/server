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
using Microsoft.AspNetCore.Identity;
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
        user.MasterPassword = "master-password";
        user.MasterPasswordSalt = "master-password-salt";
        user.Key = "old-key";
        sutProvider.GetDependency<ICurrentContext>().Organizations = [];
        ArrangeMasterPasswordServiceMutation(sutProvider);

        // Act
        var result = await sutProvider.Sut.ConvertAsync(user, wrappedUserKey);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(user.UsesKeyConnector);
        Assert.Null(user.MasterPassword);
        Assert.Null(user.MasterPasswordSalt);
        Assert.Equal(wrappedUserKey, user.Key);
        sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareClearMasterPassword(user);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .ReplaceAsync(Arg.Is<User>(u =>
                u == user &&
                u.Key == wrappedUserKey &&
                u.MasterPassword == null &&
                u.MasterPasswordSalt == null &&
                u.UsesKeyConnector));
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);
    }

    [Theory, BitAutoData]
    public async Task ConvertAsync_WrappedUserKeyNull_DoesNotOverwriteExistingKey(
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        // Arrange
        const string existingUserKey = "existing-user-key";
        user.UsesKeyConnector = false;
        user.MasterPassword = "master-password";
        user.MasterPasswordSalt = "master-password-salt";
        user.Key = existingUserKey;
        sutProvider.GetDependency<ICurrentContext>().Organizations = [];
        ArrangeMasterPasswordServiceMutation(sutProvider);

        // Act
        var result = await sutProvider.Sut.ConvertAsync(user, null);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(user.UsesKeyConnector);
        Assert.Null(user.MasterPassword);
        Assert.Null(user.MasterPasswordSalt);
        Assert.Equal(existingUserKey, user.Key);
        sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareClearMasterPassword(user);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .ReplaceAsync(Arg.Is<User>(u =>
                u == user &&
                u.Key == existingUserKey &&
                u.MasterPassword == null &&
                u.MasterPasswordSalt == null &&
                u.UsesKeyConnector));
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task ConvertAsync_WrappedUserKeyEmptyOrWhitespace_DoesNotOverwriteExistingKey(
        string wrappedUserKey,
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        // Arrange
        const string existingUserKey = "existing-user-key";
        user.UsesKeyConnector = false;
        user.MasterPassword = "master-password";
        user.MasterPasswordSalt = "master-password-salt";
        user.Key = existingUserKey;
        sutProvider.GetDependency<ICurrentContext>().Organizations = [];
        ArrangeMasterPasswordServiceMutation(sutProvider);

        // Act
        var result = await sutProvider.Sut.ConvertAsync(user, wrappedUserKey);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(existingUserKey, user.Key);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.Key == existingUserKey));
    }

    [Theory, BitAutoData]
    public async Task ConvertAsync_NullUser_Throws(
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sutProvider.Sut.ConvertAsync(null, null));

        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());
    }

    [Theory, BitAutoData]
    public async Task ConvertAsync_UserAlreadyUsesKeyConnector_ReturnsFailure(
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        user.UsesKeyConnector = true;
        sutProvider.GetDependency<ICurrentContext>().Organizations = [];

        var result = await sutProvider.Sut.ConvertAsync(user, null);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == new IdentityErrorDescriber().UserAlreadyHasPassword().Code);
        sutProvider.GetDependency<IMasterPasswordService>().DidNotReceiveWithAnyArgs()
            .PrepareClearMasterPassword(Arg.Any<User>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task ConvertAsync_UserIsNonAdminMemberOfOrg_Succeeds(
        OrganizationUserType orgUserType,
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        // Arrange
        user.UsesKeyConnector = false;
        user.MasterPassword = "master-password";
        user.MasterPasswordSalt = "master-password-salt";
        sutProvider.GetDependency<ICurrentContext>().Organizations =
        [
            new CurrentContextOrganization { Id = Guid.NewGuid(), Type = orgUserType }
        ];
        ArrangeMasterPasswordServiceMutation(sutProvider);

        // Act
        var result = await sutProvider.Sut.ConvertAsync(user, "wrapped-user-key");

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(user.UsesKeyConnector);
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u == user && u.UsesKeyConnector));
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task ConvertAsync_UserIsOwnerOrAdminOfOrg_ThrowsBadRequest(
        OrganizationUserType orgUserType,
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        user.UsesKeyConnector = false;
        sutProvider.GetDependency<ICurrentContext>().Organizations =
        [
            new CurrentContextOrganization { Id = Guid.NewGuid(), Type = orgUserType }
        ];

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConvertAsync(user, null));

        sutProvider.GetDependency<IMasterPasswordService>().DidNotReceiveWithAnyArgs()
            .PrepareClearMasterPassword(Arg.Any<User>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task ConvertAsync_UserIsMemberOfMultipleOrgsAndOwnerOrAdminOfOne_ThrowsBadRequest(
        OrganizationUserType blockingOrgUserType,
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider,
        User user)
    {
        user.UsesKeyConnector = false;
        sutProvider.GetDependency<ICurrentContext>().Organizations =
        [
            new CurrentContextOrganization { Id = Guid.NewGuid(), Type = OrganizationUserType.User },
            new CurrentContextOrganization { Id = Guid.NewGuid(), Type = blockingOrgUserType },
            new CurrentContextOrganization { Id = Guid.NewGuid(), Type = OrganizationUserType.Custom }
        ];

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConvertAsync(user, null));

        sutProvider.GetDependency<IMasterPasswordService>().DidNotReceiveWithAnyArgs()
            .PrepareClearMasterPassword(Arg.Any<User>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
    }

    // Configures the IMasterPasswordService mock so calling PrepareClearMasterPassword performs
    // the real mutation on the user. Tests then assert on both the call and the resulting state
    // captured in IUserRepository.ReplaceAsync.
    private static void ArrangeMasterPasswordServiceMutation(
        SutProvider<ConvertUserToKeyConnectorCommand> sutProvider)
    {
        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareClearMasterPassword(Arg.Do<User>(u =>
            {
                u.MasterPassword = null;
                u.MasterPasswordSalt = null;
            }))
            .Returns(call => (User)call[0]);
    }
}
