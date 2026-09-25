using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PreAccess;

public class PreAccessPolicyEnforcerTests
{
    private static readonly IPolicyRequirementFactory<IPolicyRequirement>[] _factories =
    [
        new SingleOrganizationPolicyRequirementFactory(),
        new RequireTwoFactorPolicyRequirementFactory(),
        new AutomaticUserConfirmationPolicyRequirementFactory(),
        new ResetPasswordPolicyRequirementFactory()
    ];

    [Theory]
    [BitAutoData(PolicyType.SingleOrg)]
    [BitAutoData(PolicyType.TwoFactorAuthentication)]
    [BitAutoData(PolicyType.AutomaticUserConfirmation)]
    [BitAutoData(PolicyType.ResetPassword)]
    public void Evaluate_PolicyEnabled_NotExempt_ReturnsEnforced(PolicyType policyType, Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId, Policies(CreatePolicy(organizationId, policyType)), [], _factories);

        // Act
        var result = sut.Evaluate(policyType, userId, OrganizationUserType.User);

        // Assert
        Assert.True(result.Enforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_NoPolicies_ReturnsNotEnforced(Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId, NoPolicies, [], _factories);

        // Act
        var result = sut.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.User);

        // Assert
        Assert.False(result.Enforced);
    }

    [Theory]
    [BitAutoData(PolicyType.SingleOrg, OrganizationUserType.Owner)]
    [BitAutoData(PolicyType.SingleOrg, OrganizationUserType.Admin)]
    [BitAutoData(PolicyType.TwoFactorAuthentication, OrganizationUserType.Owner)]
    [BitAutoData(PolicyType.TwoFactorAuthentication, OrganizationUserType.Admin)]
    public void Evaluate_ExemptRole_ReturnsNotEnforced(
        PolicyType policyType, OrganizationUserType proposedRole, Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId, Policies(CreatePolicy(organizationId, policyType)), [], _factories);

        // Act
        var result = sut.Evaluate(policyType, userId, proposedRole);

        // Assert
        Assert.False(result.Enforced);
    }

    [Theory]
    [BitAutoData(PolicyType.SingleOrg, OrganizationUserType.Custom)]
    [BitAutoData(PolicyType.TwoFactorAuthentication, OrganizationUserType.Custom)]
    [BitAutoData(PolicyType.AutomaticUserConfirmation, OrganizationUserType.Owner)]
    [BitAutoData(PolicyType.AutomaticUserConfirmation, OrganizationUserType.Admin)]
    [BitAutoData(PolicyType.ResetPassword, OrganizationUserType.Owner)]
    [BitAutoData(PolicyType.ResetPassword, OrganizationUserType.Admin)]
    public void Evaluate_NonExemptRole_ReturnsEnforced(
        PolicyType policyType, OrganizationUserType proposedRole, Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId, Policies(CreatePolicy(organizationId, policyType)), [], _factories);

        // Act
        var result = sut.Evaluate(policyType, userId, proposedRole);

        // Assert
        Assert.True(result.Enforced);
    }

    [Theory]
    [BitAutoData(PolicyType.SingleOrg)]
    [BitAutoData(PolicyType.TwoFactorAuthentication)]
    public void Evaluate_ProviderUser_ExemptPolicy_ReturnsNotEnforced(
        PolicyType policyType, Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId,
            Policies(CreatePolicy(organizationId, policyType)), [userId], _factories);

        // Act
        var result = sut.Evaluate(policyType, userId, OrganizationUserType.User);

        // Assert
        Assert.False(result.Enforced);
    }

    [Theory]
    [BitAutoData(PolicyType.AutomaticUserConfirmation)]
    [BitAutoData(PolicyType.ResetPassword)]
    public void Evaluate_ProviderUser_NonExemptPolicy_ReturnsEnforced(
        PolicyType policyType, Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId,
            Policies(CreatePolicy(organizationId, policyType)), [userId], _factories);

        // Act
        var result = sut.Evaluate(policyType, userId, OrganizationUserType.User);

        // Assert
        Assert.True(result.Enforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_OtherUserIsProvider_ReturnsEnforced(Guid organizationId, Guid userId, Guid providerUserId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId,
            Policies(CreatePolicy(organizationId, PolicyType.SingleOrg)), [providerUserId], _factories);

        // Act
        var result = sut.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.User);

        // Assert
        Assert.True(result.Enforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_Enforced_ReturnsPolicyData(Guid organizationId, Guid userId)
    {
        // Arrange
        var policy = CreatePolicy(organizationId, PolicyType.ResetPassword);
        policy.Data = CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        var sut = new PreAccessPolicyEnforcer(organizationId, Policies(policy), [], _factories);

        // Act
        var result = sut.Evaluate(PolicyType.ResetPassword, userId, OrganizationUserType.User);

        // Assert
        Assert.True(result.Enforced);
        Assert.True(result.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled);
    }

    [Theory, BitAutoData]
    public void Evaluate_PassesFutureStateToFactory(Guid organizationId, Guid userId)
    {
        // Arrange
        const PolicyType policyType = PolicyType.SingleOrg;
        const OrganizationUserType proposedRole = OrganizationUserType.Custom;
        const OrganizationUserStatusType expectedStatus = OrganizationUserStatusType.Accepted;
        const string policyData = "{\"some\":\"data\"}";

        var policy = CreatePolicy(organizationId, policyType);
        policy.Data = policyData;
        var factory = Substitute.For<IPolicyRequirementFactory<IPolicyRequirement>>();
        factory.PolicyType.Returns(policyType);
        factory.Enforce(Arg.Any<PolicyDetails>()).Returns(true);
        var sut = new PreAccessPolicyEnforcer(organizationId, Policies(policy), [userId], [factory]);

        // Act
        var result = sut.Evaluate(policyType, userId, proposedRole);

        // Assert
        factory.Received(1).Enforce(Arg.Is<PolicyDetails>(pd =>
            pd.OrganizationId == organizationId &&
            pd.PolicyType == policyType &&
            pd.PolicyData == policyData &&
            pd.OrganizationUserType == proposedRole &&
            pd.OrganizationUserStatus == expectedStatus &&
            pd.IsProvider));
        Assert.True(result.Enforced);
        Assert.Equal(policyData, result.Data);
    }

    [Theory, BitAutoData]
    public void Evaluate_CanEvaluateManyUsersAndPolicies(Guid organizationId, Guid userId, Guid providerUserId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId,
            Policies(CreatePolicy(organizationId, PolicyType.SingleOrg)),
            [providerUserId], _factories);

        // Act & Assert
        Assert.True(sut.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.User).Enforced);
        Assert.False(sut.Evaluate(PolicyType.SingleOrg, providerUserId, OrganizationUserType.User).Enforced);
        Assert.False(sut.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.Admin).Enforced);
        Assert.False(sut.Evaluate(PolicyType.TwoFactorAuthentication, userId, OrganizationUserType.User).Enforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_NoFactoryRegistered_Throws(Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(organizationId, NoPolicies, [], []);

        // Act & Assert
        var exception = Assert.Throws<NotImplementedException>(
            () => sut.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.User));
        Assert.Contains("No Requirement Factory found", exception.Message);
    }

    private static readonly IReadOnlyDictionary<PolicyType, Policy> NoPolicies = new Dictionary<PolicyType, Policy>();

    private static Dictionary<PolicyType, Policy> Policies(params Policy[] policies) =>
        policies.ToDictionary(p => p.Type);

    private static Policy CreatePolicy(Guid organizationId, PolicyType type) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Type = type, Enabled = true };
}
