using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
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
        organizationUser.Type = OrganizationUserType.User;

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { organizationUser.Id, true } });

        sutProvider.GetDependency<IOrganizationService>()
            .HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>(), includeProvider: true)
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
    public async Task DeleteUserAsync_WithUserNotFound_ThrowsNotFoundException(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        Guid organizationId, Guid organizationUserId)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns((OrganizationUser)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_WithUserNotManaged_ThrowsBadRequestException(
        SutProvider<DeleteManagedOrganizationUserAccountCommand> sutProvider,
        OrganizationUser organizationUser)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { organizationUser.Id, false } });

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, null));
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

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser1.Id, true }, { orgUser2.Id, true } });

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user1.Id).Returns(user1);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user2.Id).Returns(user2);

        // Act
        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, new[] { orgUser1.Id, orgUser2.Id }, null);

        // Assert
        Assert.Equal(2, results.Count());
        Assert.All(results, r => Assert.Empty(r.Item2));

        await sutProvider.GetDependency<IUserService>().Received(1).DeleteAsync(user1);
        await sutProvider.GetDependency<IUserService>().Received(1).DeleteAsync(user2);
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser1, EventType.OrganizationUser_Deleted);
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser2, EventType.OrganizationUser_Deleted);
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

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { orgUser1.Id, true }, { orgUser3.Id, false } });

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user1.Id).Returns(user1);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user3.Id).Returns(user3);

        // Act
        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, new[] { orgUser1.Id, orgUser2.Id, orgUser3.Id }, null);

        // Assert
        Assert.Equal(3, results.Count());
        Assert.Empty(results.First(r => r.Item1 == orgUser1.Id).Item2);
        Assert.Equal("You cannot delete a user with Invited status.", results.First(r => r.Item1 == orgUser2.Id).Item2);
        Assert.Equal("User is not managed by the organization.", results.First(r => r.Item1 == orgUser3.Id).Item2);

        await sutProvider.GetDependency<IUserService>().Received(1).DeleteAsync(user1);
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser1, EventType.OrganizationUser_Deleted);
    }
}
