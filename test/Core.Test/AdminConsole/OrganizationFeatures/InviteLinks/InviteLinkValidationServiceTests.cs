using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class InviteLinkValidationServiceTests
{
    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenAllChecksPass_ReturnsNoError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = Substitute.For<IPreAccessPolicyEnforcer>();
        List<OrganizationUser> allOrganizationMemberships = [];
        SetupAllChecksPass(user, allOrganizationMemberships, sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.Admin,
            policyEnforcer);

        // Assert
        Assert.Null(result.Error);
        Assert.False(result.AutoConfirmPolicyEnabled);

        var joinOrganizationPolicyValidationService = sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>();

        await sutProvider.GetDependency<IOrganizationUserValidationService>()
            .Received(1)
            .ValidateFreeOrgAdminLimitAsync(
                user.Id,
                organization.PlanType,
                OrganizationUserType.User,
                OrganizationUserType.Admin);
        await joinOrganizationPolicyValidationService
            .Received(1)
            .ValidateAutomaticUserConfirmationPolicyAsync(
                organization.Id,
                user,
                OrganizationUserType.Admin,
                policyEnforcer,
                allOrganizationMemberships);
        await joinOrganizationPolicyValidationService
            .Received(1)
            .ValidateSingleOrganizationPolicyAsync(
                organization.Id,
                user,
                OrganizationUserType.Admin,
                policyEnforcer,
                allOrganizationMemberships);
        await joinOrganizationPolicyValidationService
            .Received(1)
            .ValidateTwoFactorAuthenticationPolicyAsync(
                organization.Id,
                user,
                OrganizationUserType.Admin,
                policyEnforcer);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenFreeAdminLimitReached_ReturnsError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .ValidateFreeOrgAdminLimitAsync(
                Arg.Any<Guid?>(),
                Arg.Any<PlanType>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<OrganizationUserType>())
            .Returns(new CannotBeAdminOfMultipleFreeOrganizations());

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.Admin,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.IsType<CannotBeAdminOfMultipleFreeOrganizations>(result.Error);
        Assert.False(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenAutoConfirmPolicyFails_ReturnsError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateAutomaticUserConfirmationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns(new AutomaticUserConfirmationPolicyValidationResult(
                new UserCannotBelongToAnotherOrganization(user.Email),
                AutoConfirmPolicyEnabled: true));

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.User,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.Error);
        Assert.True(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenSingleOrgPolicyFails_ReturnsError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateSingleOrganizationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns(new UserIsAMemberOfAnotherOrganization());

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.User,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenTwoFactorPolicyFails_ReturnsError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateTwoFactorAuthenticationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>())
            .Returns(new TwoFactorRequiredForMembership());

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.User,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.IsType<TwoFactorRequiredForMembership>(result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenAutoConfirmPolicyEnabled_ReturnsAutoConfirmPolicyEnabled(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateAutomaticUserConfirmationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns(new AutomaticUserConfirmationPolicyValidationResult(null, AutoConfirmPolicyEnabled: true));

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.User,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.Null(result.Error);
        Assert.True(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateSeatAvailabilityAsync_WithNewMembershipAndNoSeats_ReturnsNoAvailableSeats(
        Organization organization,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupNoAvailableSeats(organization, sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateSeatAvailabilityAsync(organization, existingOrganizationUser: null);

        // Assert
        var error = Assert.IsType<OrganizationHasNoAvailableSeats>(result);
        Assert.Equal(organization.DisplayName(), error.OrgName);
    }

    [Theory, BitAutoData]
    public async Task ValidateSeatAvailabilityAsync_WithStagedMembershipAndNoSeats_ReturnsNoAvailableSeats(
        Organization organization,
        OrganizationUser existingOrganizationUser,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupNoAvailableSeats(organization, sutProvider);
        existingOrganizationUser.Status = OrganizationUserStatusType.Staged;

        // Act
        var result = await sutProvider.Sut.ValidateSeatAvailabilityAsync(organization, existingOrganizationUser);

        // Assert
        var error = Assert.IsType<OrganizationHasNoAvailableSeats>(result);
        Assert.Equal(organization.DisplayName(), error.OrgName);
    }

    // An invited membership already takes up a seat, so a full organization doesn't block it.
    [Theory, BitAutoData]
    public async Task ValidateSeatAvailabilityAsync_WithInvitedMembershipAndNoSeats_ReturnsNoError(
        Organization organization,
        OrganizationUser existingOrganizationUser,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupNoAvailableSeats(organization, sutProvider);
        existingOrganizationUser.Status = OrganizationUserStatusType.Invited;

        // Act
        var result = await sutProvider.Sut.ValidateSeatAvailabilityAsync(organization, existingOrganizationUser);

        // Assert
        Assert.Null(result);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetOccupiedSeatCountByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ValidateSeatAvailabilityAsync_WithNewMembershipAndSeatsAvailable_ReturnsNoError(
        Organization organization,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = 10;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 9 });

        // Act
        var result = await sutProvider.Sut.ValidateSeatAvailabilityAsync(organization, existingOrganizationUser: null);

        // Assert
        Assert.Null(result);
    }

    // Fills the organization's seats with no room left to auto-scale.
    private static void SetupNoAvailableSeats(
        Organization organization,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = 10;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 10 });
    }

    private static void SetupAllChecksPass(
        User user,
        List<OrganizationUser> allOrganizationMemberships,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(allOrganizationMemberships);

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .ValidateFreeOrgAdminLimitAsync(
                Arg.Any<Guid?>(),
                Arg.Any<PlanType>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<OrganizationUserType>())
            .Returns((Error?)null);

        var joinOrganizationPolicyValidationService = sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>();

        joinOrganizationPolicyValidationService
            .ValidateAutomaticUserConfirmationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns(new AutomaticUserConfirmationPolicyValidationResult(null, AutoConfirmPolicyEnabled: false));

        joinOrganizationPolicyValidationService
            .ValidateSingleOrganizationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns((Error?)null);

        joinOrganizationPolicyValidationService
            .ValidateTwoFactorAuthenticationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>())
            .Returns((Error?)null);
    }
}
