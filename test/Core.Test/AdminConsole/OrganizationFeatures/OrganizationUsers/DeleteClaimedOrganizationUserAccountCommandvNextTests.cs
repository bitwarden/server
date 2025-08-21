using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class DeleteClaimedOrganizationUserAccountCommandvNextTests
{
    // [Theory]
    // [BitAutoData]
    // public async Task DeleteUserAsync_WithValidUser_DeletesUserAndLogsEvent(
    //     SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider, User user, Guid deletingUserId,
    //     [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    // {
    //     // Arrange
    //     organizationUser.UserId = user.Id;

    //     sutProvider.GetDependency<IUserRepository>()
    //         .GetByIdAsync(user.Id)
    //         .Returns(user);

    //     sutProvider.GetDependency<IOrganizationUserRepository>()
    //         .GetByIdAsync(organizationUser.Id)
    //         .Returns(organizationUser);

    //     sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
    //         .GetUsersOrganizationClaimedStatusAsync(
    //             organizationUser.OrganizationId,
    //             Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)))
    //         .Returns(new Dictionary<Guid, bool> { { organizationUser.Id, true } });

    //     sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
    //         .HasConfirmedOwnersExceptAsync(
    //             organizationUser.OrganizationId,
    //             Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)),
    //             includeProvider: Arg.Any<bool>())
    //         .Returns(true);

    //     // Act
    //     await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId);

    //     // Assert
    //     await sutProvider.GetDependency<IUserService>().Received(1).DeleteAsync(user);
    //     await sutProvider.GetDependency<IEventService>().Received(1)
    //         .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Deleted);
    // }


    // [Theory]
    // [BitAutoData]
    // public async Task DeleteManyUsersAsync_WithValidUsers_DeletesUsersAndLogsEvents(
    //     SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider, User user1, User user2, Guid organizationId,
    //     [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser1,
    //     [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser2)
    // {
    //     // Arrange
    //     orgUser1.OrganizationId = orgUser2.OrganizationId = organizationId;
    //     orgUser1.UserId = user1.Id;
    //     orgUser2.UserId = user2.Id;

    //     sutProvider.GetDependency<IOrganizationUserRepository>()
    //         .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
    //         .Returns(new List<OrganizationUser> { orgUser1, orgUser2 });

    //     sutProvider.GetDependency<IUserRepository>()
    //         .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user1.Id) && ids.Contains(user2.Id)))
    //         .Returns(new[] { user1, user2 });

    //     sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
    //         .GetUsersOrganizationClaimedStatusAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
    //         .Returns(new Dictionary<Guid, bool> { { orgUser1.Id, true }, { orgUser2.Id, true } });

    //     // Act
    //     var userIds = new[] { orgUser1.Id, orgUser2.Id };
    //     var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, userIds, null);

    //     // Assert
    //     Assert.Equal(2, results.Count());
    //     Assert.All(results, r => Assert.Empty(r.Item2));

    //     await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyAsync(userIds);
    //     await sutProvider.GetDependency<IUserRepository>().Received(1).DeleteManyAsync(Arg.Is<IEnumerable<User>>(users => users.Any(u => u.Id == user1.Id) && users.Any(u => u.Id == user2.Id)));
    //     await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(
    //         Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
    //             events.Count(e => e.Item1.Id == orgUser1.Id && e.Item2 == EventType.OrganizationUser_Deleted) == 1
    //             && events.Count(e => e.Item1.Id == orgUser2.Id && e.Item2 == EventType.OrganizationUser_Deleted) == 1));
    // }





}
