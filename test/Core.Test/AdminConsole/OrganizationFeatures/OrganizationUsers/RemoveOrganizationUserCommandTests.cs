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
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class RemoveOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task RemoveUser_WithDeletingUserId_Success(
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = deletingUser.OrganizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(deletingUser.Id)
            .Returns(deletingUser);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(deletingUser.OrganizationId)
            .Returns(true);

        // Act
        await sutProvider.Sut.RemoveUserAsync(deletingUser.OrganizationId, organizationUser.Id, deletingUser.UserId);

        // Assert
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetUsersOrganizationManagementStatusAsync(default, default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .DeleteAsync(organizationUser);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithDeletingUserId_WithAccountDeprovisioningEnabled_Success(
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = deletingUser.OrganizationId;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(deletingUser.Id)
            .Returns(deletingUser);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(deletingUser.OrganizationId)
            .Returns(true);

        // Act
        await sutProvider.Sut.RemoveUserAsync(deletingUser.OrganizationId, organizationUser.Id, deletingUser.UserId);

        // Assert
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .Received(1)
            .GetUsersOrganizationManagementStatusAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)));
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .DeleteAsync(organizationUser);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithDeletingUserId_NotFound_ThrowsException(
        SutProvider<RemoveOrganizationUserCommand> sutProvider,
        Guid organizationId, Guid organizationUserId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, null));
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithDeletingUserId_MismatchingOrganizationId_ThrowsException(
        SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, null));
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithDeletingUserId_InvalidUser_ThrowsException(
        OrganizationUser organizationUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RemoveUserAsync(Guid.NewGuid(), organizationUser.Id, null));
        Assert.Contains(RemoveOrganizationUserCommand.UserNotFoundErrorMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithDeletingUserId_RemoveYourself_ThrowsException(
        OrganizationUser deletingUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(deletingUser.Id)
            .Returns(deletingUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(deletingUser.OrganizationId, deletingUser.Id, deletingUser.UserId));
        Assert.Contains(RemoveOrganizationUserCommand.RemoveYourselfErrorMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithDeletingUserId_NonOwnerRemoveOwner_ThrowsException(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
        [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = deletingUser.OrganizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationAdmin(organizationUser.OrganizationId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUser.UserId));
        Assert.Contains(RemoveOrganizationUserCommand.RemoveOwnerByNonOwnerErrorMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithDeletingUserId_RemovingLastOwner_ThrowsException(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
        OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = deletingUser.OrganizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)),
                Arg.Any<bool>())
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(deletingUser.OrganizationId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUser.UserId));
        Assert.Contains(RemoveOrganizationUserCommand.RemoveLastConfirmedOwnerErrorMessage, exception.Message);
        await sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .Received(1)
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)), true);
    }

    [Theory, BitAutoData]
    public async Task RemoveUserAsync_WithDeletingUserId_WithAccountDeprovisioningEnabled_WhenUserIsManaged_ThrowsException(
        [OrganizationUser(status: OrganizationUserStatusType.Confirmed)] OrganizationUser orgUser,
        Guid deletingUserId,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser.Id)
            .Returns(orgUser);
        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(orgUser.OrganizationId, Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser.Id)))
            .Returns(new Dictionary<Guid, bool> { { orgUser.Id, true } });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(orgUser.OrganizationId, orgUser.Id, deletingUserId));
        Assert.Contains(RemoveOrganizationUserCommand.RemoveClaimedAccountErrorMessage, exception.Message);
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .Received(1)
            .GetUsersOrganizationManagementStatusAsync(orgUser.OrganizationId, Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser.Id)));
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithEventSystemUser_Success(
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        EventSystemUser eventSystemUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act
        await sutProvider.Sut.RemoveUserAsync(organizationUser.OrganizationId, organizationUser.Id, eventSystemUser);

        // Assert
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetUsersOrganizationManagementStatusAsync(default, default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .DeleteAsync(organizationUser);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed, eventSystemUser);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithEventSystemUser_WithAccountDeprovisioningEnabled_Success(
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        EventSystemUser eventSystemUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act
        await sutProvider.Sut.RemoveUserAsync(organizationUser.OrganizationId, organizationUser.Id, eventSystemUser);

        // Assert
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetUsersOrganizationManagementStatusAsync(default, default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .DeleteAsync(organizationUser);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed, eventSystemUser);
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveUser_WithEventSystemUser_NotFound_ThrowsException(
        SutProvider<RemoveOrganizationUserCommand> sutProvider,
        Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, eventSystemUser));
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveUser_WithEventSystemUser_MismatchingOrganizationId_ThrowsException(
        SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, eventSystemUser));
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithEventSystemUser_RemovingLastOwner_ThrowsException(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
        EventSystemUser eventSystemUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)),
                Arg.Any<bool>())
            .Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(organizationUser.OrganizationId, organizationUser.Id, eventSystemUser));
        Assert.Contains(RemoveOrganizationUserCommand.RemoveLastConfirmedOwnerErrorMessage, exception.Message);
        await sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .Received(1)
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)), true);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_ByUserId_Success(
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository
            .GetByOrganizationAsync(organizationUser.OrganizationId, organizationUser.UserId!.Value)
            .Returns(organizationUser);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)),
                Arg.Any<bool>())
            .Returns(true);

        await sutProvider.Sut.RemoveUserAsync(organizationUser.OrganizationId, organizationUser.UserId.Value);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_ByUserId_NotFound_ThrowsException(
        SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RemoveUserAsync(organizationId, userId));
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_ByUserId_InvalidUser_ThrowsException(
        OrganizationUser organizationUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationUser.OrganizationId, organizationUser.UserId!.Value)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RemoveUserAsync(Guid.NewGuid(), organizationUser.UserId.Value));
        Assert.Contains(RemoveOrganizationUserCommand.UserNotFoundErrorMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_ByUserId_RemovingLastOwner_ThrowsException(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationUser.OrganizationId, organizationUser.UserId!.Value)
            .Returns(organizationUser);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)),
                Arg.Any<bool>())
            .Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(organizationUser.OrganizationId, organizationUser.UserId.Value));
        Assert.Contains(RemoveOrganizationUserCommand.RemoveLastConfirmedOwnerErrorMessage, exception.Message);
        await sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .Received(1)
            .HasConfirmedOwnersExceptAsync(
                organizationUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)),
                Arg.Any<bool>());
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithDeletingUserId_Success(
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1, OrganizationUser orgUser2)
    {
        // Arrange
        var sutProvider = SutProviderFactory();
        var eventDate = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime;
        orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
        var organizationUsers = new[] { orgUser1, orgUser2 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(deletingUser.Id)
            .Returns(deletingUser);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(deletingUser.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(
                deletingUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser1.Id) && i.Contains(orgUser2.Id)))
            .Returns(new Dictionary<Guid, bool> { { orgUser1.Id, false }, { orgUser2.Id, false } });

        // Act
        var result = await sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, r => Assert.Empty(r.ErrorMessage));
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetUsersOrganizationManagementStatusAsync(default, default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser1.Id) && i.Contains(orgUser2.Id)));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(
                Arg.Is<IEnumerable<(OrganizationUser OrganizationUser, EventType EventType, DateTime? DateTime)>>(i =>
                    i.First().OrganizationUser.Id == orgUser1.Id
                    && i.Last().OrganizationUser.Id == orgUser2.Id
                    && i.All(u => u.DateTime == eventDate)));
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithDeletingUserId_WithAccountDeprovisioningEnabled_Success(
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1, OrganizationUser orgUser2)
    {
        // Arrange
        var sutProvider = SutProviderFactory();
        var eventDate = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime;
        orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
        var organizationUsers = new[] { orgUser1, orgUser2 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(deletingUser.Id)
            .Returns(deletingUser);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(deletingUser.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(
                deletingUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser1.Id) && i.Contains(orgUser2.Id)))
            .Returns(new Dictionary<Guid, bool> { { orgUser1.Id, false }, { orgUser2.Id, false } });

        // Act
        var result = await sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, r => Assert.Empty(r.ErrorMessage));
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .Received(1)
            .GetUsersOrganizationManagementStatusAsync(
                deletingUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser1.Id) && i.Contains(orgUser2.Id)));
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser1.Id) && i.Contains(orgUser2.Id)));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(
                Arg.Is<IEnumerable<(OrganizationUser OrganizationUser, EventType EventType, DateTime? DateTime)>>(i =>
                    i.First().OrganizationUser.Id == orgUser1.Id
                    && i.Last().OrganizationUser.Id == orgUser2.Id
                    && i.All(u => u.DateTime == eventDate)));
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithDeletingUserId_WithMismatchingOrganizationId_ThrowsException(OrganizationUser organizationUser,
        OrganizationUser deletingUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        var organizationUsers = new[] { organizationUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId));
        Assert.Contains(RemoveOrganizationUserCommand.UsersInvalidErrorMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithDeletingUserId_RemoveYourself_ThrowsException(
        OrganizationUser deletingUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        var organizationUsers = new[] { deletingUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);

        // Assert
        Assert.Contains(RemoveOrganizationUserCommand.RemoveYourselfErrorMessage, result.First().ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithDeletingUserId_NonOwnerRemoveOwner_ThrowsException(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser orgUser2,
        [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
        var organizationUsers = new[] { orgUser1, orgUser2 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);

        // Assert
        Assert.Contains(RemoveOrganizationUserCommand.RemoveOwnerByNonOwnerErrorMessage, result.First().ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithDeletingUserId_RemovingManagedUser_WithAccountDeprovisioningEnabled_ThrowsException(
        [OrganizationUser(status: OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        orgUser.OrganizationId = deletingUser.OrganizationId;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser.Id)))
            .Returns(new[] { orgUser });

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .GetUsersOrganizationManagementStatusAsync(orgUser.OrganizationId, Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser.Id)))
            .Returns(new Dictionary<Guid, bool> { { orgUser.Id, true } });

        // Act
        var result = await sutProvider.Sut.RemoveUsersAsync(orgUser.OrganizationId, new[] { orgUser.Id }, deletingUser.UserId);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser OrganizationUser, EventType EventType, DateTime? DateTime)>>());
        Assert.Contains(RemoveOrganizationUserCommand.RemoveClaimedAccountErrorMessage, result.First().ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithDeletingUserId_LastOwner_ThrowsException(
        [OrganizationUser(status: OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        var organizationUsers = new[] { orgUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(orgUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(organizationUsers);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUsersAsync(orgUser.OrganizationId, organizationUserIds, null));
        Assert.Contains(RemoveOrganizationUserCommand.RemoveLastConfirmedOwnerErrorMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithEventSystemUser_Success(
        EventSystemUser eventSystemUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1,
        OrganizationUser orgUser2)
    {
        // Arrange
        var sutProvider = SutProviderFactory();
        var eventDate = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime;
        orgUser1.OrganizationId = orgUser2.OrganizationId;
        var organizationUsers = new[] { orgUser1, orgUser2 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(orgUser1.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.RemoveUsersAsync(orgUser1.OrganizationId, organizationUserIds, eventSystemUser);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, r => Assert.Empty(r.ErrorMessage));
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetUsersOrganizationManagementStatusAsync(default, default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser1.Id) && i.Contains(orgUser2.Id)));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(
                Arg.Is<IEnumerable<(OrganizationUser OrganizationUser, EventType EventType, EventSystemUser EventSystemUser, DateTime? DateTime)>>(
                    i => i.First().OrganizationUser.Id == orgUser1.Id
                        && i.Last().OrganizationUser.Id == orgUser2.Id
                        && i.All(u => u.EventSystemUser == eventSystemUser
                            && u.DateTime == eventDate)));
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithEventSystemUser_WithAccountDeprovisioningEnabled_Success(
        EventSystemUser eventSystemUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1,
        OrganizationUser orgUser2)
    {
        // Arrange
        var sutProvider = SutProviderFactory();
        var eventDate = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime;
        orgUser1.OrganizationId = orgUser2.OrganizationId;
        var organizationUsers = new[] { orgUser1, orgUser2 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(orgUser1.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.RemoveUsersAsync(orgUser1.OrganizationId, organizationUserIds, eventSystemUser);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, r => Assert.Empty(r.ErrorMessage));
        await sutProvider.GetDependency<IGetOrganizationUsersManagementStatusQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetUsersOrganizationManagementStatusAsync(default, default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(orgUser1.Id) && i.Contains(orgUser2.Id)));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(
                Arg.Is<IEnumerable<(OrganizationUser OrganizationUser, EventType EventType, EventSystemUser EventSystemUser, DateTime? DateTime)>>(
                    i => i.First().OrganizationUser.Id == orgUser1.Id
                        && i.Last().OrganizationUser.Id == orgUser2.Id
                        && i.All(u => u.EventSystemUser == eventSystemUser
                            && u.DateTime == eventDate)));
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithEventSystemUser_WithMismatchingOrganizationId_ThrowsException(
        EventSystemUser eventSystemUser,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        var organizationUsers = new[] { organizationUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUsersAsync(Guid.NewGuid(), organizationUserIds, eventSystemUser));
        Assert.Contains(RemoveOrganizationUserCommand.UsersInvalidErrorMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_WithEventSystemUser_LastOwner_ThrowsException(
        [OrganizationUser(status: OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
        EventSystemUser eventSystemUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        // Arrange
        var organizationUsers = new[] { orgUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(default)
            .ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(orgUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(organizationUsers);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUsersAsync(orgUser.OrganizationId, organizationUserIds, eventSystemUser));
        Assert.Contains(RemoveOrganizationUserCommand.RemoveLastConfirmedOwnerErrorMessage, exception.Message);
    }

    /// <summary>
    /// Returns a new SutProvider with a FakeTimeProvider registered in the Sut.
    /// </summary>
    private static SutProvider<RemoveOrganizationUserCommand> SutProviderFactory()
    {
        return new SutProvider<RemoveOrganizationUserCommand>()
            .WithFakeTimeProvider()
            .Create();
    }
}
