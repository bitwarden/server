using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

[SutProviderCustomize]
public class JoinOrganizationPolicyValidationServiceTests
{
    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_WithNoPolicies_ReturnsNull(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        SetupValidDependencies(organization, user, sutProvider);
        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.Null(result);
    }

    // ----- Single Organization -----

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_AnotherOrganizationEnforcesSingleOrg_ReturnsError(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        SetupValidDependencies(organization, user, sutProvider);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([PolicyDetailForOtherOrganization(PolicyType.SingleOrg)]));

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_TargetSingleOrgAndMemberOfAnotherOrganization_ReturnsError(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.SingleOrg);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_TargetSingleOrgAndNoOtherMembership_ReturnsNull(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.SingleOrg);

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_EvaluatesTargetPoliciesWithProposedRole(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());

        // Act
        await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.Admin);

        // Assert
        policyEnforcer.Received(1).Evaluate(PolicyType.SingleOrg, user.Id, OrganizationUserType.Admin);
        policyEnforcer.Received(1).Evaluate(PolicyType.TwoFactorAuthentication, user.Id, OrganizationUserType.Admin);
        policyEnforcer.Received(1).Evaluate(PolicyType.AutomaticUserConfirmation, user.Id, OrganizationUserType.Admin);
    }

    // ----- Require Two-Factor Authentication -----

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_TargetTwoFactorAndUserLacksTwoFactor_ReturnsError(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.TwoFactorAuthentication);

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<TwoFactorRequiredForMembership>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_TargetTwoFactorAndUserHasTwoFactor_ReturnsNull(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.TwoFactorAuthentication);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>().TwoFactorIsEnabledAsync(user).Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.Null(result);
    }

    // ----- Automatic User Confirmation -----

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_AnotherOrganizationEnforcesAutoConfirm_ReturnsError(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        SetupValidDependencies(organization, user, sutProvider);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement(
                [PolicyDetailForOtherOrganization(PolicyType.AutomaticUserConfirmation)]));

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<OtherOrganizationDoesNotAllowOtherMembership>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_TargetAutoConfirmAndProviderUser_ReturnsError(
        Organization organization, User user, ProviderUser providerUser,
        SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);
        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([providerUser]);

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<ProviderUsersCannotJoin>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_TargetAutoConfirmAndMemberOfAnotherOrganization_ReturnsError(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateCanJoinAsync_TargetAutoConfirmAndNoConflicts_ReturnsNull(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);

        // Act
        var result = await sutProvider.Sut.ValidateCanJoinAsync(organization, user, OrganizationUserType.User);

        // Assert
        Assert.Null(result);
    }

    private static void EnableTargetPolicy(IPreAccessPolicyEnforcer policyEnforcer, PolicyType policyType) =>
        policyEnforcer
            .Evaluate(policyType, Arg.Any<Guid>(), Arg.Any<OrganizationUserType>())
            .Returns(PreAccessPolicyDecision.Enforced(null));

    private static void SetMemberships(
        User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider, params OrganizationUser[] memberships) =>
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(memberships.ToList());

    private static OrganizationUser MembershipInOtherOrganization() => new() { OrganizationId = Guid.NewGuid() };

    private static PolicyDetails PolicyDetailForOtherOrganization(PolicyType policyType) =>
        new()
        {
            OrganizationId = Guid.NewGuid(),
            PolicyType = policyType,
            OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
        };

    // Valid baseline: no policies anywhere, no other memberships, not a provider user, no 2FA enabled.
    // Returns the organization's policy enforcer, which enforces nothing until a test enables a policy on it.
    private static IPreAccessPolicyEnforcer SetupValidDependencies(
        Organization organization, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        user.Email = "user@example.com";

        var policyEnforcer = Substitute.For<IPreAccessPolicyEnforcer>();
        policyEnforcer
            .Evaluate(Arg.Any<PolicyType>(), Arg.Any<Guid>(), Arg.Any<OrganizationUserType>())
            .Returns(PreAccessPolicyDecision.NotEnforced);
        sutProvider.GetDependency<IPreAccessEnforcerQuery>()
            .RunAsync(organization.Id)
            .Returns(policyEnforcer);

        SetMemberships(user, sutProvider);
        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([]);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        return policyEnforcer;
    }
}
