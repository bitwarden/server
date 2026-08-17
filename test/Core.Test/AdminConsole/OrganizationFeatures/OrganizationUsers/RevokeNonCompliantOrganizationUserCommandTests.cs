using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
        var command = new RevokeOrganizationUsersRequest(organizationId, [], new InvalidUser(), RevocationReason.TwoFactorPolicyNonCompliance);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.ErrorRequestedByWasNotValid, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeThemselves_ThenErrorShouldBeReturned(
            Guid organizationId, OrganizationUserUserDetails revokingUser,
            SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        var command = new RevokeOrganizationUsersRequest(organizationId, revokingUser,
            CreateActingUser(revokingUser?.UserId ?? Guid.NewGuid(), OrganizationUserType.Owner), RevocationReason.TwoFactorPolicyNonCompliance);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.ErrorCannotRevokeSelf, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeOrgUsersFromAnotherOrg_ThenErrorShouldBeReturned(
            Guid organizationId, OrganizationUserUserDetails userFromAnotherOrg,
            SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userFromAnotherOrg.OrganizationId = Guid.NewGuid();

        var command = new RevokeOrganizationUsersRequest(organizationId, userFromAnotherOrg,
            CreateActingUser(Guid.NewGuid(), OrganizationUserType.Owner), RevocationReason.TwoFactorPolicyNonCompliance);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.ErrorInvalidUsers, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeAllOwnersFromOrg_ThenErrorShouldBeReturned(
            Guid organizationId, OrganizationUserUserDetails userToRevoke,
            SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;

        var command = new RevokeOrganizationUsersRequest(organizationId, userToRevoke,
            CreateActingUser(Guid.NewGuid(), OrganizationUserType.Owner), RevocationReason.TwoFactorPolicyNonCompliance);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(false);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains(RevokeNonCompliantOrganizationUserCommand.ErrorOrgMustHaveAtLeastOneOwner, result.ErrorMessages);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeOwnerWhenNotAnOwner_ThenErrorShouldBeReturned(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Type = OrganizationUserType.Owner;

        UseRealValidationService(sutProvider);
        var command = new RevokeOrganizationUsersRequest(organizationId, userToRevoke,
            CreateActingUser(Guid.NewGuid(), OrganizationUserType.Admin), RevocationReason.TwoFactorPolicyNonCompliance);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains("only an owner can manage another owner's account.", result.ErrorMessages.Select(m => m.ToLowerInvariant()));
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenCustomUserAttemptsToRevokeAdmin_ThenErrorShouldBeReturned(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Type = OrganizationUserType.Admin;

        UseRealValidationService(sutProvider);
        var command = new RevokeOrganizationUsersRequest(organizationId, userToRevoke,
            CreateActingUser(Guid.NewGuid(), OrganizationUserType.Custom, new Permissions { ManageUsers = true }),
            RevocationReason.TwoFactorPolicyNonCompliance);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains("custom users can not manage admins or owners.", result.ErrorMessages.Select(m => m.ToLowerInvariant()));
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenPopulatedRequest_WhenUserAttemptsToRevokeUserWhoIsAlreadyRevoked_ThenErrorShouldBeReturned(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Status = OrganizationUserStatusType.Revoked;

        UseRealValidationService(sutProvider);
        var command = new RevokeOrganizationUsersRequest(organizationId, userToRevoke,
            CreateActingUser(Guid.NewGuid(), OrganizationUserType.Owner), RevocationReason.TwoFactorPolicyNonCompliance);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
        Assert.Contains($"{RevokeNonCompliantOrganizationUserCommand.ErrorUserAlreadyRevoked} Id: {userToRevoke.Id}", result.ErrorMessages);
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

        UseRealValidationService(sutProvider);
        var command = new RevokeOrganizationUsersRequest(organizationId, revocableUsers,
            CreateActingUser(Guid.NewGuid(), OrganizationUserType.User), RevocationReason.TwoFactorPolicyNonCompliance);

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

        UseRealValidationService(sutProvider);
        var command = new RevokeOrganizationUsersRequest(organizationId, userToRevoke,
            CreateActingUser(Guid.NewGuid(), OrganizationUserType.Admin), RevocationReason.TwoFactorPolicyNonCompliance);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RevokeManyAsync(Arg.Is<IEnumerable<Guid>>(x => x.Count() == 1 && x.Contains(userToRevoke.Id)), RevocationReason.TwoFactorPolicyNonCompliance);

        Assert.True(result.Success);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(
                Arg.Is<IEnumerable<(OrganizationUserUserDetails organizationUser, EventType eventType, DateTime? time
                    )>>(
                    x => x.Any(y =>
                        y.organizationUser.Id == userToRevoke.Id && y.eventType == EventType.OrganizationUser_Revoked_TwoFactorNonCompliance)
                ));
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenOwnerActingUserWithNoOrganizationUserType_WhenRevokingAUser_ThenUserShouldBeRevoked(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Type = OrganizationUserType.Admin;

        UseRealValidationService(sutProvider);
        var command = new RevokeOrganizationUsersRequest(organizationId, userToRevoke,
            new StandardUser(Guid.NewGuid(), isOrganizationOwner: true), RevocationReason.TwoFactorPolicyNonCompliance);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.Success);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenNonOwnerActingUserWithNoOrganizationUserType_WhenRevokingAUser_ThenErrorShouldBeReturned(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Type = OrganizationUserType.User;

        UseRealValidationService(sutProvider);
        var command = new RevokeOrganizationUsersRequest(organizationId, userToRevoke,
            new StandardUser(Guid.NewGuid(), isOrganizationOwner: false), RevocationReason.TwoFactorPolicyNonCompliance);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.HasErrors);
    }

    [Theory, BitAutoData]
    public async Task RevokeNonCompliantOrganizationUsersAsync_GivenSystemUser_WhenRevokingAnOwner_ThenUserShouldBeRevoked(
        Guid organizationId, OrganizationUserUserDetails userToRevoke,
        SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        userToRevoke.OrganizationId = organizationId;
        userToRevoke.Type = OrganizationUserType.Owner;

        var command = new RevokeOrganizationUsersRequest(organizationId, userToRevoke,
            new SystemUser(EventSystemUser.SCIM), RevocationReason.TwoFactorPolicyNonCompliance);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var result = await sutProvider.Sut.RevokeNonCompliantOrganizationUsersAsync(command);

        Assert.True(result.Success);
    }

    /// <summary>
    /// Replaces the auto-mocked <see cref="IOrganizationUserValidationService"/> with a real instance (backed by a
    /// provider-repository stub reporting no provider memberships), so these tests exercise the real "can manage"
    /// hierarchy instead of re-implementing it as a mock.
    /// </summary>
    private static void UseRealValidationService(SutProvider<RevokeNonCompliantOrganizationUserCommand> sutProvider)
    {
        var providerUserRepository = Substitute.For<IProviderUserRepository>();
        providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(Arg.Any<Guid>(), ProviderUserStatusType.Confirmed)
            .Returns([]);

        var validationService = new OrganizationUserValidationService(
            providerUserRepository, Substitute.For<IOrganizationUserRepository>());

        sutProvider.SetDependency<IOrganizationUserValidationService>(validationService, "organizationUserValidationService");
        sutProvider.Create();
    }

    private static IActingUser CreateActingUser(Guid userId, OrganizationUserType type, Permissions? permissions = null) =>
        new StandardUser(userId, type == OrganizationUserType.Owner, type, permissions);

    public class InvalidUser : IActingUser
    {
        public Guid? UserId => Guid.Empty;
        public bool IsOrganizationOwnerOrProvider => false;
        public EventSystemUser? SystemUserType => null;
        public Permissions? Permissions => null;
        public OrganizationUserType? OrganizationUserType => null;
    }
}
