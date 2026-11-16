using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
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
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

[SutProviderCustomize]
public class JoinOrganizationPolicyValidationServiceTests
{
    private const string _validResetPasswordKey = "2.validresetpasswordkey";

    // ----- Single Organization -----

    [Theory, BitAutoData]
    public async Task ValidateSingleOrganizationPolicyAsync_WithNoPolicies_ReturnsNull(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupSingleOrganizationDependencies(user, sutProvider);

        // Act
        var error = await sutProvider.Sut.ValidateSingleOrganizationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, [MembershipIn(Guid.NewGuid())]);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateSingleOrganizationPolicyAsync_AnotherOrganizationEnforcesSingleOrg_ReturnsError(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupSingleOrganizationDependencies(user, sutProvider);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([PolicyDetailForOtherOrganization(PolicyType.SingleOrg)]));

        // Act
        var error = await sutProvider.Sut.ValidateSingleOrganizationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, [MembershipIn(Guid.NewGuid())]);

        // Assert
        Assert.IsType<UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateSingleOrganizationPolicyAsync_TargetEnforcesAndMemberOfAnotherOrganization_ReturnsError(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupSingleOrganizationDependencies(user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.SingleOrg);

        // Act
        var error = await sutProvider.Sut.ValidateSingleOrganizationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, [MembershipIn(Guid.NewGuid())]);

        // Assert
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateSingleOrganizationPolicyAsync_TargetEnforcesAndOnlyMemberOfThisOrganization_ReturnsNull(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupSingleOrganizationDependencies(user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.SingleOrg);

        // Act
        var error = await sutProvider.Sut.ValidateSingleOrganizationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, [MembershipIn(organizationId)]);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateSingleOrganizationPolicyAsync_WithOrganizationId_LoadsEnforcerAndMemberships(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupSingleOrganizationDependencies(user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.SingleOrg);
        SetupLoadedData(organizationId, user, sutProvider, policyEnforcer, MembershipIn(Guid.NewGuid()));

        // Act
        var error = await sutProvider.Sut.ValidateSingleOrganizationPolicyAsync(
            organizationId, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(error);
        await sutProvider.GetDependency<IPreAccessEnforcerQuery>().Received(1).RunAsync(organizationId);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyByUserAsync(user.Id);
    }

    // ----- Require Two-Factor Authentication -----

    [Theory, BitAutoData]
    public async Task ValidateTwoFactorAuthenticationPolicyAsync_WithNoPolicy_ReturnsNull(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();

        // Act
        var error = await sutProvider.Sut.ValidateTwoFactorAuthenticationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateTwoFactorAuthenticationPolicyAsync_TargetEnforcesAndUserLacksTwoFactor_ReturnsError(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();
        EnableTargetPolicy(policyEnforcer, PolicyType.TwoFactorAuthentication);

        // Act
        var error = await sutProvider.Sut.ValidateTwoFactorAuthenticationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer);

        // Assert
        Assert.IsType<TwoFactorRequiredForMembership>(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateTwoFactorAuthenticationPolicyAsync_TargetEnforcesAndUserHasTwoFactor_ReturnsNull(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();
        EnableTargetPolicy(policyEnforcer, PolicyType.TwoFactorAuthentication);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>().TwoFactorIsEnabledAsync(user).Returns(true);

        // Act
        var error = await sutProvider.Sut.ValidateTwoFactorAuthenticationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateTwoFactorAuthenticationPolicyAsync_WithOrganizationId_LoadsEnforcerOnly(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();
        EnableTargetPolicy(policyEnforcer, PolicyType.TwoFactorAuthentication);
        SetupLoadedData(organizationId, user, sutProvider, policyEnforcer);

        // Act
        var error = await sutProvider.Sut.ValidateTwoFactorAuthenticationPolicyAsync(
            organizationId, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<TwoFactorRequiredForMembership>(error);
        await sutProvider.GetDependency<IPreAccessEnforcerQuery>().Received(1).RunAsync(organizationId);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByUserAsync(default);
    }

    // ----- Automatic User Confirmation -----

    [Theory, BitAutoData]
    public async Task ValidateAutomaticUserConfirmationPolicyAsync_WithNoPolicies_ReturnsAutoConfirmPolicyDisabled(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupAutomaticUserConfirmationDependencies(user, sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAutomaticUserConfirmationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, [MembershipIn(Guid.NewGuid())]);

        // Assert
        Assert.False(result.IsError);
        Assert.False(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateAutomaticUserConfirmationPolicyAsync_AnotherOrganizationEnforces_ReturnsError(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupAutomaticUserConfirmationDependencies(user, sutProvider);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement(
                [PolicyDetailForOtherOrganization(PolicyType.AutomaticUserConfirmation)]));

        // Act
        var result = await sutProvider.Sut.ValidateAutomaticUserConfirmationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, []);

        // Assert
        Assert.IsType<OtherOrganizationDoesNotAllowOtherMembership>(result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAutomaticUserConfirmationPolicyAsync_TargetEnforcesAndProviderUser_ReturnsError(
        Guid organizationId, User user, ProviderUser providerUser,
        SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupAutomaticUserConfirmationDependencies(user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);
        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([providerUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAutomaticUserConfirmationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, []);

        // Assert
        Assert.IsType<ProviderUsersCannotJoin>(result.Error);
        Assert.True(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateAutomaticUserConfirmationPolicyAsync_TargetEnforcesAndMemberOfAnotherOrganization_ReturnsError(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupAutomaticUserConfirmationDependencies(user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);

        // Act
        var result = await sutProvider.Sut.ValidateAutomaticUserConfirmationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, [MembershipIn(Guid.NewGuid())]);

        // Assert
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.Error);
        Assert.True(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateAutomaticUserConfirmationPolicyAsync_TargetEnforcesAndNoConflicts_ReturnsAutoConfirmPolicyEnabledWithoutError(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupAutomaticUserConfirmationDependencies(user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);

        // Act
        var result = await sutProvider.Sut.ValidateAutomaticUserConfirmationPolicyAsync(
            organizationId, user, OrganizationUserType.User, policyEnforcer, [MembershipIn(organizationId)]);

        // Assert
        // The caller relies on AutoConfirmPolicyEnabled to perform the policy's side effects.
        Assert.False(result.IsError);
        Assert.True(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateAutomaticUserConfirmationPolicyAsync_WithOrganizationId_LoadsEnforcerAndMemberships(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = SetupAutomaticUserConfirmationDependencies(user, sutProvider);
        EnableTargetPolicy(policyEnforcer, PolicyType.AutomaticUserConfirmation);
        SetupLoadedData(organizationId, user, sutProvider, policyEnforcer, MembershipIn(Guid.NewGuid()));

        // Act
        var result = await sutProvider.Sut.ValidateAutomaticUserConfirmationPolicyAsync(
            organizationId, user, OrganizationUserType.User);

        // Assert
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.Error);
        await sutProvider.GetDependency<IPreAccessEnforcerQuery>().Received(1).RunAsync(organizationId);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).GetManyByUserAsync(user.Id);
    }

    // ----- Account Recovery -----

    [Theory, BitAutoData]
    public void ValidateAccountRecoveryPolicy_WithNoPolicy_ReturnsNoAutoEnroll(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();

        // Act
        var result = sutProvider.Sut.ValidateAccountRecoveryPolicy(
            organizationId, user, OrganizationUserType.User, policyEnforcer, resetPasswordKey: null);

        // Assert
        Assert.False(result.IsError);
        Assert.False(result.AutoEnrollEnabled);
    }

    [Theory, BitAutoData]
    public void ValidateAccountRecoveryPolicy_WithoutAutoEnroll_DoesNotRequireKey(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();
        EnableAccountRecoveryPolicy(policyEnforcer, autoEnrollEnabled: false);

        // Act
        var result = sutProvider.Sut.ValidateAccountRecoveryPolicy(
            organizationId, user, OrganizationUserType.User, policyEnforcer, resetPasswordKey: null);

        // Assert
        Assert.False(result.IsError);
        Assert.False(result.AutoEnrollEnabled);
    }

    [Theory, BitAutoData]
    public void ValidateAccountRecoveryPolicy_WithAutoEnrollAndMissingKey_ReturnsError(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();
        EnableAccountRecoveryPolicy(policyEnforcer, autoEnrollEnabled: true);

        // Act
        var result = sutProvider.Sut.ValidateAccountRecoveryPolicy(
            organizationId, user, OrganizationUserType.User, policyEnforcer, resetPasswordKey: null);

        // Assert
        Assert.IsType<ResetPasswordKeyRequired>(result.Error);
        Assert.True(result.AutoEnrollEnabled);
    }

    [Theory, BitAutoData]
    public void ValidateAccountRecoveryPolicy_WithAutoEnrollAndValidKey_ReturnsAutoEnrollWithoutError(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();
        EnableAccountRecoveryPolicy(policyEnforcer, autoEnrollEnabled: true);

        // Act
        var result = sutProvider.Sut.ValidateAccountRecoveryPolicy(
            organizationId, user, OrganizationUserType.User, policyEnforcer, _validResetPasswordKey);

        // Assert
        // The caller relies on AutoEnrollEnabled to enroll the user once they join.
        Assert.False(result.IsError);
        Assert.True(result.AutoEnrollEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateAccountRecoveryPolicyAsync_WithOrganizationId_LoadsEnforcerOnly(
        Guid organizationId, User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = CreatePolicyEnforcer();
        EnableAccountRecoveryPolicy(policyEnforcer, autoEnrollEnabled: true);
        SetupLoadedData(organizationId, user, sutProvider, policyEnforcer);

        // Act
        var result = await sutProvider.Sut.ValidateAccountRecoveryPolicyAsync(
            organizationId, user, OrganizationUserType.User, resetPasswordKey: null);

        // Assert
        Assert.IsType<ResetPasswordKeyRequired>(result.Error);
        await sutProvider.GetDependency<IPreAccessEnforcerQuery>().Received(1).RunAsync(organizationId);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByUserAsync(default);
    }

    private static void EnableTargetPolicy(IPreAccessPolicyEnforcer policyEnforcer, PolicyType policyType) =>
        policyEnforcer
            .Evaluate(policyType, Arg.Any<Guid>(), Arg.Any<OrganizationUserType>())
            .Returns(PreAccessPolicyDecision.Enforced(null));

    private static void EnableAccountRecoveryPolicy(IPreAccessPolicyEnforcer policyEnforcer, bool autoEnrollEnabled) =>
        policyEnforcer
            .Evaluate(PolicyType.ResetPassword, Arg.Any<Guid>(), Arg.Any<OrganizationUserType>())
            .Returns(PreAccessPolicyDecision.Enforced(
                CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = autoEnrollEnabled })));

    private static OrganizationUser MembershipIn(Guid organizationId) => new() { OrganizationId = organizationId };

    private static PolicyDetails PolicyDetailForOtherOrganization(PolicyType policyType) =>
        new()
        {
            OrganizationId = Guid.NewGuid(),
            PolicyType = policyType,
            OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
        };

    /// <summary>
    /// Makes the overloads that take an organization id load <paramref name="policyEnforcer"/> and
    /// <paramref name="memberships"/>.
    /// </summary>
    private static void SetupLoadedData(Guid organizationId, User user,
        SutProvider<JoinOrganizationPolicyValidationService> sutProvider, IPreAccessPolicyEnforcer policyEnforcer,
        params OrganizationUser[] memberships)
    {
        sutProvider.GetDependency<IPreAccessEnforcerQuery>().RunAsync(organizationId).Returns(policyEnforcer);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(memberships.ToList());
    }

    // Enforces nothing until a test enables a policy on it.
    private static IPreAccessPolicyEnforcer CreatePolicyEnforcer()
    {
        var policyEnforcer = Substitute.For<IPreAccessPolicyEnforcer>();
        policyEnforcer
            .Evaluate(Arg.Any<PolicyType>(), Arg.Any<Guid>(), Arg.Any<OrganizationUserType>())
            .Returns(PreAccessPolicyDecision.NotEnforced);
        return policyEnforcer;
    }

    private static IPreAccessPolicyEnforcer SetupSingleOrganizationDependencies(
        User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));
        return CreatePolicyEnforcer();
    }

    private static IPreAccessPolicyEnforcer SetupAutomaticUserConfirmationDependencies(
        User user, SutProvider<JoinOrganizationPolicyValidationService> sutProvider)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IProviderUserRepository>().GetManyByUserAsync(user.Id).Returns([]);
        return CreatePolicyEnforcer();
    }
}
