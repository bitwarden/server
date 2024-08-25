using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
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
public class DeleteOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteUser_Success(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = organizationId
            });

        await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).DeleteUserAsync(organizationId, organizationUserId, null);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_NotFound_Throws(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_MismatchingOrganizationId_Throws(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, null));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithEventSystemUser_Success(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(new OrganizationUser
            {
                Id = organizationUserId,
                OrganizationId = organizationId
            });

        await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, eventSystemUser);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).DeleteUserAsync(organizationId, organizationUserId, eventSystemUser);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_AdminRemoved_Success(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, true, true);

        await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId, OrganizationUserRemovalType.AdminRemove);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).DeleteAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id));
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id), EventType.OrganizationUser_Removed);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_AdminDelete_ManagedUser_ConfirmedStatus_WithUserId_Success(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User, status: OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        organizationUser.UserId = Guid.NewGuid();
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, true, true);

        await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId, OrganizationUserRemovalType.AdminDelete);

        await sutProvider.GetDependency<IUserService>().Received(1).DeleteAsync(Arg.Is<User>(u => u.Id == organizationUser.UserId));
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id), EventType.OrganizationUser_Deleted);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_AdminDelete_ManagedUser_ConfirmedStatus_WithoutUserId_Success(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User, status: OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, true, true);
        organizationUser.UserId = null;

        await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId, OrganizationUserRemovalType.AdminDelete);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).DeleteAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id));
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id), EventType.OrganizationUser_Deleted);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_AdminDelete_ManagedUser_RevokedStatus_WithUserId_Success(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User, status: OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        organizationUser.UserId = Guid.NewGuid();
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, true, true);

        await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId, OrganizationUserRemovalType.AdminDelete);

        await sutProvider.GetDependency<IUserService>().Received(1).DeleteAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id), EventType.OrganizationUser_Deleted);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_AdminDelete_ManagedUser_RevokedStatus_WithoutUserId_Success(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User, status: OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, true, true);
        organizationUser.UserId = null;

        await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId, OrganizationUserRemovalType.AdminDelete);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).DeleteAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id));
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id), EventType.OrganizationUser_Deleted);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_AdminDelete_ManagedUser_InvalidStatus_ThrowsException(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User, status: OrganizationUserStatusType.Invited)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, true, true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId, OrganizationUserRemovalType.AdminDelete));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_AdminDelete_UnmanagedUser_ThrowsException(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser,
        Guid deletingUserId)
    {
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, false, true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId, OrganizationUserRemovalType.AdminDelete));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_SelfRemove_ManagedUser_ThrowsException(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, true, true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, organizationUser.UserId, OrganizationUserRemovalType.SelfRemove));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_WithAccountDeprovisioning_SelfRemove_UnmanagedUser_Success(
        SutProvider<DeleteOrganizationUserCommand> sutProvider,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        SetupAccountDeprovisioningCommonMocks(sutProvider, organizationUser, false, true);

        await sutProvider.Sut.DeleteUserAsync(organizationUser.OrganizationId, organizationUser.Id, deletingUserId: null, OrganizationUserRemovalType.SelfRemove);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).DeleteAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id));
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(Arg.Is<OrganizationUser>(ou => ou.Id == organizationUser.Id), EventType.OrganizationUser_Left);
    }

    private void SetupAccountDeprovisioningCommonMocks(SutProvider<DeleteOrganizationUserCommand> sutProvider, OrganizationUser organizationUser, bool isManaged, bool hasConfirmedOwners)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationService>()
            .GetUsersOrganizationManagementStatusAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { { organizationUser.Id, isManaged } });
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
        sutProvider.GetDependency<IOrganizationService>()
            .HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(hasConfirmedOwners);
        if (organizationUser.UserId.HasValue)
        {
            sutProvider.GetDependency<IUserRepository>()
                .GetByIdAsync(organizationUser.UserId.Value)
                .Returns(new User { Id = organizationUser.UserId.Value });
        }
    }
}
