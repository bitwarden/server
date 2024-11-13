using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class RevokeNonCompliantOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenUnrecognizedUserType_WhenAttemptingToRevoke_ThenErrorShouldBeReturned(
            Guid organizationId, SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        var command = new RevokeOrganizationUsers(organizationId, [], new InvalidUser());

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.RequestedByWasNotValid, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeThemselves_ThenErrorShouldBeReturned(
            Guid organizationId, OrganizationUserUserDetails revokingUser,
            SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        var command = new RevokeOrganizationUsers(organizationId, revokingUser,
            new StandardUser(revokingUser?.UserId ?? Guid.NewGuid(), true));

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.CannotRevokeSelfMessage, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeOrgUsersFromAnotherOrg_ThenErrorShouldBeReturned(
            Guid organizationId, OrganizationUserUserDetails userFromAnotherOrg,
            SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userFromAnotherOrg.OrganizationId = Guid.NewGuid();

        var command = new RevokeOrganizationUsers(organizationId, userFromAnotherOrg,
            new StandardUser(Guid.NewGuid(), true));

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.InvalidUsers, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeAllOwnersFromOrg_ThenErrorShouldBeReturned(
            Guid organizationId, OrganizationUserUserDetails userToRevoke,
            SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;

        var command = new RevokeOrganizationUsers(organizationId, userToRevoke,
            new StandardUser(Guid.NewGuid(), true));

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(false);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.OrgMustHaveAtLeastOneOwner, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeOwnerWhenNotAnOwner_ThenErrorShouldBeReturned(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Type = OrganizationUserType.Owner;

        var command = new RevokeOrganizationUsers(organizationId, userToRevoke,
            new StandardUser(Guid.NewGuid(), false));

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.OnlyOwnersCanRevokeOtherOwners, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeUserWhoIsAlreadyRevoked_ThenErrorShouldBeReturned(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Status = OrganizationUserStatusType.Revoked;

        var command = new RevokeOrganizationUsers(organizationId, userToRevoke,
            new StandardUser(Guid.NewGuid(), true));

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains($"{RevokeNonCompliantOrganizationUserCommand.UserAlreadyRevoked} Id: {userToRevoke.Id}", result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserHasMultipleInvalidUsers_ThenErrorShouldBeReturned(
        Guid organizationId, IEnumerable<OrganizationUserUserDetails> usersToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        var revocableUsers = usersToRevoke.ToList();
        revocableUsers.ForEach(user => user.OrganizationId = organizationId);
        revocableUsers[0].Type = OrganizationUserType.Owner;
        revocableUsers[1].Status = OrganizationUserStatusType.Revoked;

        var command = new RevokeOrganizationUsers(organizationId, revocableUsers,
            new StandardUser(Guid.NewGuid(), false));

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.True(result.ErrorMessages.Count > 1);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenValidPopulatedRequest_WhenUserAttemptsToRevokeAUser_ThenUserShouldBeRevoked(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Type = OrganizationUserType.Admin;

        var command = new RevokeOrganizationUsers(organizationId, userToRevoke,
            new StandardUser(Guid.NewGuid(), false));

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .SetOrganizationUsersStatusAsync(Arg.Any<IEnumerable<Guid>>(), OrganizationUserStatusType.Revoked);

        Assert.True(result.Success);
    }

    public class InvalidUser : IActingUser
    {
        public Guid? UserId => Guid.Empty;
        public bool IsOrganizationOwner => false;
        public EventSystemUser? SystemUserType => null;
    }
}
