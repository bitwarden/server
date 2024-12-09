using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
    public async Task DeleteUserAsync_WithValidUser_DeletesUserAndLogsEvent(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user, Guid deletingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = user.Id;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)))
            .Returns(new Dictionary<Guid, bool> { { organizationUser.Id, true } });

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)),
                includeProvider: Arg.Any<bool>())
            .Returns(true);

        // Act
        await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId);

        // Assert
        await sutProvider.GetDependency<IUserService>().Received(1).DeleteAsync(user);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Deleted);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_WithUserNotFound_ThrowsException(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        Guid organizationId, Guid organizationUserId)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns((OrganizationUser?)null);

        // Act
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null));

        // Assert
        Assert.Equal("Member not found.", exception.Message);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<DateTime?>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_DeletingYourself_ThrowsException(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        User user,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        // Arrange
        organizationUser.UserId = user.Id = deletingUserId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id)
            .Returns(user);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId));

        // Assert
        Assert.Equal("You cannot delete yourself.", exception.Message);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<DateTime?>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_WhenUserIsInvited_ThrowsException(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Invited, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, null));

        // Assert
        Assert.Equal("You cannot delete a member with Invited status.", exception.Message);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<DateTime?>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_DeletingOwnerWhenNotOwner_ThrowsException(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        // Arrange
        organizationUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationUser.OrganizationId)
            .Returns(false);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId));

        // Assert
        Assert.Equal("Only owners can delete other owners.", exception.Message);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<DateTime?>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_DeletingLastConfirmedOwner_ThrowsException(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        // Arrange
        organizationUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationUser.OrganizationId)
            .Returns(true);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)),
                includeProvider: Arg.Any<bool>())
            .Returns(false);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId));

        // Assert
        Assert.Equal("Organization must have at least one confirmed owner.", exception.Message);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<DateTime?>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_WithUserNotManaged_ThrowsException(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider, User user,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        // Arrange
        organizationUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { organizationUser.Id, false } });

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, null));

        // Assert
        Assert.Equal("Member is not managed by the organization.", exception.Message);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<DateTime?>());
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
        Assert.All(results, r => Assert.Empty(r.Item2));

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
        Assert.Equal(orgUserId, result.First().Item1);
        Assert.Contains("Member not found.", result.First().Item2);
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
        Assert.Equal(orgUser.Id, result.First().Item1);
        Assert.Contains("You cannot delete yourself.", result.First().Item2);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenUserIsInvited_ReturnsErrorMessage(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Invited, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.UserId = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUser });

        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, null);

        // Assert
        Assert.Single(result);
        Assert.Equal(orgUser.Id, result.First().Item1);
        Assert.Contains("You cannot delete a member with Invited status.", result.First().Item2);
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

        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, deletingUserId);

        Assert.Single(result);
        Assert.Equal(orgUser.Id, result.First().Item1);
        Assert.Contains("Only owners can delete other owners.", result.First().Item2);
        await sutProvider.GetDependency<IUserService>().Received(0).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(0)
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WhenDeletingLastOwner_ReturnsErrorMessage(
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
            .Returns(true);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>(), Arg.Any<bool>())
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.DeleteManyUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, deletingUserId);

        // Assert
        Assert.Single(result);
        Assert.Equal(orgUser.Id, result.First().Item1);
        Assert.Contains("Organization must have at least one confirmed owner.", result.First().Item2);
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
        Assert.Equal(orgUser.Id, result.First().Item1);
        Assert.Contains("Member is not managed by the organization.", result.First().Item2);
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
        orgUser1.OrganizationId = orgUser2.OrganizationId = orgUser3.OrganizationId = organizationId;

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
        Assert.Empty(results.First(r => r.Item1 == orgUser1.Id).Item2);
        Assert.Equal("You cannot delete a member with Invited status.", results.First(r => r.Item1 == orgUser2.Id).Item2);
        Assert.Equal("Member is not managed by the organization.", results.First(r => r.Item1 == orgUser3.Id).Item2);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(
            Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
            events.Count(e => e.Item1.Id == orgUser1.Id && e.Item2 == EventType.OrganizationUser_Deleted) == 1));
    }
}
