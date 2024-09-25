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
public class RemoveOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task RemoveUser_Success(
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationUser.OrganizationId = deletingUser.OrganizationId;
        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
        organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);
        currentContext.OrganizationOwner(deletingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.RemoveUserAsync(deletingUser.OrganizationId, organizationUser.Id, deletingUser.UserId);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_WithEventSystemUser_Success(
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        EventSystemUser eventSystemUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);

        await sutProvider.Sut.RemoveUserAsync(organizationUser.OrganizationId, organizationUser.Id, eventSystemUser);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed, eventSystemUser);
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveUser_NotFound_Throws(SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, null));
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveUser_MismatchingOrganizationId_Throws(SutProvider<RemoveOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RemoveUserAsync(organizationId, organizationUserId, null));
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_InvalidUser(OrganizationUser organizationUser, OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RemoveUserAsync(Guid.NewGuid(), organizationUser.Id, deletingUser.UserId));
        Assert.Contains("User not found.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_RemoveYourself(OrganizationUser deletingUser, SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(deletingUser.OrganizationId, deletingUser.Id, deletingUser.UserId));
        Assert.Contains("You cannot remove yourself.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_NonOwnerRemoveOwner(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
        [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationUser.OrganizationId = deletingUser.OrganizationId;
        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
        currentContext.OrganizationAdmin(deletingUser.OrganizationId).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(deletingUser.OrganizationId, organizationUser.Id, deletingUser.UserId));
        Assert.Contains("Only owners can delete other owners.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUser_RemovingLastOwner_ThrowsException(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
        OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var hasConfirmedOwnersExceptQuery = sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>();

        organizationUser.OrganizationId = deletingUser.OrganizationId;
        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
        hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, new[] { organizationUser.Id }, true).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUserAsync(deletingUser.OrganizationId, organizationUser.Id, null));
        Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
        hasConfirmedOwnersExceptQuery.Received(1).HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.Id)), true);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_FilterInvalid(OrganizationUser organizationUser, OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationUsers = new[] { organizationUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId));
        Assert.Contains("Users invalid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_RemoveYourself(
        OrganizationUser deletingUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationUsers = new[] { deletingUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
        Assert.Contains("You cannot remove yourself.", result[0].Item2);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_NonOwnerRemoveOwner(
        [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser orgUser2,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
        var organizationUsers = new[] { orgUser1 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
        Assert.Contains("Only owners can delete other owners.", result[0].Item2);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_LastOwner(
        [OrganizationUser(status: OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var organizationUsers = new[] { orgUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
        organizationUserRepository.GetManyByOrganizationAsync(orgUser.OrganizationId, OrganizationUserType.Owner).Returns(organizationUsers);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveUsersAsync(orgUser.OrganizationId, organizationUserIds, null));
        Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveUsers_Success(
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1, OrganizationUser orgUser2,
        SutProvider<RemoveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
        var organizationUsers = new[] { orgUser1, orgUser2 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
        organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(deletingUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
        currentContext.OrganizationOwner(deletingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.RemoveUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
    }
}
