using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Commands;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class DeleteManagedOrganizationUserAccountCommandTests
{

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_WithValidUser_DeletesUserAndLogsEvents(
       SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user, Guid organizationId,
       [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new[] { user });

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser.Id, true } });

        // Act
        await sutProvider.Sut.DeleteUserAsync(organizationId, orgUser.Id, null);

        // Assert
        await sutProvider.GetDependency<IUserRepository>().Received(1).DeleteManyAsync(Arg.Is<IEnumerable<User>>(users => users.Any(u => u.Id == user.Id)));
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(
            Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                events.Count(e => e.Item1.Id == orgUser.Id && e.Item2 == EventType.OrganizationUser_Deleted) == 1));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_WhenError_ShouldReturnFailure(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user, Guid organizationId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { });

        // Act
        // Test is not ready
        await sutProvider.Sut.DeleteUserAsync(organizationId, orgUser.Id, null);

        // Assert
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WithValidUsers_DeletesUsersAndLogsEvents(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user1, User user2, Guid organizationId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser2)
    {
        // Arrange
        orgUser1.OrganizationId = orgUser2.OrganizationId = organizationId;
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = user2.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser1, orgUser2 });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user1.Id) && ids.Contains(user2.Id)))
            .Returns(new[] { user1, user2 });

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser1.Id, true }, { orgUser2.Id, true } });

        // Act
        var userIds = new[] { orgUser1.Id, orgUser2.Id };
        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, userIds, null);

        // Assert
        Assert.Equal(2, results.Count());
        Assert.All(results, r => Assert.IsType<Success>(r.Item2));

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyAsync(userIds);
        await sutProvider.GetDependency<IUserRepository>().Received(1).DeleteManyAsync(Arg.Is<IEnumerable<User>>(users => users.Any(u => u.Id == user1.Id) && users.Any(u => u.Id == user2.Id)));
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(
            Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                events.Count(e => e.Item1.Id == orgUser1.Id && e.Item2 == EventType.OrganizationUser_Deleted) == 1
                && events.Count(e => e.Item1.Id == orgUser2.Id && e.Item2 == EventType.OrganizationUser_Deleted) == 1));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenUserNotFound_ReturnsErrorMessage(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        Guid organizationId,
        Guid orgUserId)
    {
        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, new[] { orgUserId }, null);

        // Assert
        Assert.Single(result);

        var userId = result.First().Item1;
        Assert.Equal(orgUserId, userId);

        var commandResult = result.First().Item2;
        AssertErrorMessages("Member not found.", commandResult);

        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenDeletingYourself_ReturnsErrorMessage(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        User user, [OrganizationUser] OrganizationUser orgUser, Guid deletingUserId)
    {
        // Arrange
        orgUser.UserId = user.Id = deletingUserId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new[] { user });

        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, deletingUserId);

        // Assert
        Assert.Single(result);

        var userId = result.First().Item1;
        Assert.Equal(orgUser.Id, userId);

        var commandResult = result.First().Item2;
        AssertErrorMessages("You cannot delete yourself.", commandResult);

        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenUserIsInvited_ReturnsErrorMessage(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        User user,
        [OrganizationUser(OrganizationUserStatusType.Invited, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(input => input.Contains(user.Id)))
            .Returns(new[] { user });

        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, null);

        // Assert
        Assert.Single(result);
        var userId = result.First().Item1;
        Assert.Equal(orgUser.Id, userId);
        AssertErrorMessages("You cannot delete a member with Invited status.", result.First().Item2);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenDeletingOwnerAsNonOwner_ReturnsErrorMessage(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
        Guid deletingUserId)
    {
        // Arrange
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(user.Id)))
            .Returns(new[] { user });

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(orgUser.OrganizationId)
            .Returns(false);

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser.Id, true } });


        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, deletingUserId);

        // Assert
        Assert.Single(result);

        var userId = result.First().Item1;
        Assert.Equal(orgUser.Id, userId);

        var commandResult = result.First().Item2;
        AssertErrorMessages("Only owners can delete other owners.", commandResult);

        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenUserNotManaged_ReturnsErrorMessage(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser.UserId.Value)))
            .Returns(new[] { user });

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser.Id, false } });

        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, null);

        // Assert
        Assert.Single(result);

        var userId = result.First().Item1;
        Assert.Equal(orgUser.Id, userId);

        var commandResult = result.First().Item2;
        AssertErrorMessages("Member is not managed by the organization.", commandResult);

        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }


    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenUserIsASoleOwner_ReturnsErrorMessage(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser.UserId.Value)))
            .Returns(new[] { user });

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser.Id, true } });

        const int onlyOwnerCount = 1;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByOnlyOwnerAsync(Arg.Is<Guid>(id => id == user.Id))
            .Returns(onlyOwnerCount);

        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, null);

        // Assert
        Assert.Single(result);

        var userId = result.First().Item1;
        Assert.Equal(orgUser.Id, userId);

        var commandResult = result.First().Item2;
        AssertErrorMessages("Cannot delete this user because it is the sole owner of at least one organization. Please delete these organizations or upgrade another user.", commandResult);

        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenUserIsASoleProvider_ReturnsErrorMessage(
       SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user,
       [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser.UserId.Value)))
            .Returns(new[] { user });

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser.Id, true } });

        const int onlyOwnerCount = 0;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByOnlyOwnerAsync(Arg.Is<Guid>(id => id == user.Id))
            .Returns(onlyOwnerCount);

        const int onlyOwnerProviderCount = 1;

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetCountByOnlyOwnerAsync(Arg.Is<Guid>(id => id == user.Id))
            .Returns(onlyOwnerProviderCount);

        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, null);

        // Assert
        Assert.Single(result);

        var userId = result.First().Item1;
        Assert.Equal(orgUser.Id, userId);

        var commandResult = result.First().Item2;
        AssertErrorMessages("Cannot delete this user because it is the sole owner of at least one provider. Please delete these providers or upgrade another user.", commandResult);

        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_MixedValidAndInvalidUsers_ReturnsAppropriateResults(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user1, User user3,
        Guid organizationId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Invited, OrganizationUserType.User)] OrganizationUser orgUser2,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser3)
    {
        // Arrange
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = null;
        orgUser3.UserId = user3.Id;
        orgUser1.OrganizationId = organizationId;
        orgUser2.OrganizationId = organizationId;
        orgUser3.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser1, orgUser2, orgUser3 });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user1.Id) && ids.Contains(user3.Id)))
            .Returns(new[] { user1, user3 });

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser1.Id, true }, { orgUser3.Id, false } });

        // Act
        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, new[] { orgUser1.Id, orgUser2.Id, orgUser3.Id }, null);

        // Assert
        Assert.Equal(3, results.Count());

        var orgUser1ErrorMessage = results.First(r => r.Item1 == orgUser1.Id).Item2;
        Assert.Null(orgUser1ErrorMessage);

        var orgUser2CommandResult = results.First(r => r.Item1 == orgUser2.Id).Item2;
        AssertErrorMessages("Member not found.", orgUser2CommandResult);

        var orgUser3CommandResult = results.First(r => r.Item1 == orgUser3.Id).Item2;
        AssertErrorMessages("Member is not managed by the organization.", orgUser3CommandResult);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(
            Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
            events.Count(e => e.Item1.Id == orgUser1.Id && e.Item2 == EventType.OrganizationUser_Deleted) == 1));
    }

    private static void AssertErrorMessages(string expectedErrorMessage, CommandResult commandResult) => Assert.Contains([expectedErrorMessage], ((Failure)commandResult).ErrorMessages.ToArray());
}
