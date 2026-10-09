using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
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

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class InviteLinkPolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WithNoPolicies_ReturnsValidRequest(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        SetupValidDependencies(organization, user, sutProvider);
        var request = BuildRequest(organization, user);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Same(request, result.Request);
    }

    // ----- Single Organization -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_AnotherOrganizationEnforcesSingleOrg_ReturnsError(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        SetupValidDependencies(organization, user, sutProvider);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([PolicyDetailForOtherOrganization(PolicyType.SingleOrg)]));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TargetSingleOrgAndMemberOfAnotherOrganization_ReturnsError(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.SingleOrg);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TargetSingleOrgAndNoOtherMembership_ReturnsValidRequest(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.SingleOrg);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingMember_EvaluatesTargetPoliciesWithMembershipRole(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());

        // Act
        await sutProvider.Sut.ValidateAsync(
            BuildRequest(organization, user, Membership(OrganizationUserStatusType.Invited, OrganizationUserType.Admin)));

        // Assert
        policyEnforcer.Received(1).Evaluate(PolicyType.SingleOrg, user.Id, OrganizationUserType.Admin);
        policyEnforcer.Received(1).Evaluate(PolicyType.TwoFactorAuthentication, user.Id, OrganizationUserType.Admin);
        policyEnforcer.Received(1).Evaluate(PolicyType.AutomaticUserConfirmation, user.Id, OrganizationUserType.Admin);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NewMember_EvaluatesTargetPoliciesAsUser(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());

        // Act
        await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        policyEnforcer.Received(1).Evaluate(PolicyType.SingleOrg, user.Id, OrganizationUserType.User);
        policyEnforcer.Received(1).Evaluate(PolicyType.TwoFactorAuthentication, user.Id, OrganizationUserType.User);
        policyEnforcer.Received(1).Evaluate(PolicyType.AutomaticUserConfirmation, user.Id, OrganizationUserType.User);
    }

    // ----- Require Two-Factor Authentication -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_TargetTwoFactorAndUserLacksTwoFactor_ReturnsError(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.TwoFactorAuthentication);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TargetTwoFactorAndUserHasTwoFactor_ReturnsValidRequest(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.TwoFactorAuthentication);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>().TwoFactorIsEnabledAsync(user).Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsValid);
    }

    // ----- Automatic User Confirmation -----

    [Theory, BitAutoData]
    public async Task ValidateAsync_AnotherOrganizationEnforcesAutoConfirm_ReturnsError(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        SetupValidDependencies(organization, user, sutProvider);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement(
                [PolicyDetailForOtherOrganization(PolicyType.AutomaticUserConfirmation)]));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OtherOrganizationDoesNotAllowOtherMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TargetAutoConfirmAndProviderUser_ReturnsError(
        Organization organization, User user, ProviderUser providerUser,
        SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);
        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([providerUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ProviderUsersCannotAcceptInviteLink>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TargetAutoConfirmAndMemberOfAnotherOrganization_ReturnsError(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);
        SetMemberships(user, sutProvider, MembershipInOtherOrganization());

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TargetAutoConfirmAndNoConflicts_ReturnsValidRequest(
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupValidDependencies(organization, user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user));

        // Assert
        Assert.True(result.IsValid);
    }

    private static InviteLinkPolicyValidationRequest BuildRequest(
        Organization organization, User user, OrganizationUser? existingOrganizationUser = null) =>
        new()
        {
            Organization = organization,
            User = user,
            ExistingOrganizationUser = existingOrganizationUser,
        };

    private static void EnableTargetPolicy(IPreAccessPolicyEnforcer policyEnforcer, PolicyType policyType) =>
        policyEnforcer
            .Evaluate(policyType, Arg.Any<Guid>(), Arg.Any<OrganizationUserType>())
            .Returns(PreAccessPolicyDecision.Enforced(null));

    private static void SetMemberships(
        User user, SutProvider<InviteLinkPolicyValidator> sutProvider, params OrganizationUser[] memberships) =>
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(memberships.ToList());

    private static OrganizationUser MembershipInOtherOrganization() => new() { OrganizationId = Guid.NewGuid() };

    private static OrganizationUser Membership(
        OrganizationUserStatusType status, OrganizationUserType type = OrganizationUserType.User) =>
        new() { Status = status, Type = type };

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
        Organization organization, User user, SutProvider<InviteLinkPolicyValidator> sutProvider)
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
