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
    // The policy types are arbitrary; they only identify which test factory handles each policy.
    private const PolicyType ExemptingPolicyType = PolicyType.SingleOrg;
    private const PolicyType NonExemptingPolicyType = PolicyType.TwoFactorAuthentication;

    private static readonly IPolicyRequirementFactory<IPolicyRequirement>[] _factories =
    [
        // This exempt role is intentionally unusual to make sure we're not relying on the base class defaults
        new TestPreAccessPolicyRequirementFactory(ExemptingPolicyType, [OrganizationUserType.Custom], exemptProviders: true),
        new TestPreAccessPolicyRequirementFactory(NonExemptingPolicyType, [], exemptProviders: false)
    ];

    [Theory]
    [BitAutoData(ExemptingPolicyType)]
    [BitAutoData(NonExemptingPolicyType)]
    public void Evaluate_PolicyEnabled_NotExempt_ReturnsEnforced(PolicyType policyType, Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(Policies(CreatePolicy(organizationId, policyType)), [], _factories);

        // Act
        var decision = sut.Evaluate(policyType, userId, OrganizationUserType.User);

        // Assert
        Assert.True(decision.IsEnforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_NoPolicies_ReturnsNotEnforced(Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(NoPolicies, [], _factories);

        // Act
        var decision = sut.Evaluate(NonExemptingPolicyType, userId, OrganizationUserType.User);

        // Assert
        Assert.False(decision.IsEnforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_PolicyDisabled_ReturnsNotEnforced(Guid organizationId, Guid userId)
    {
        // Arrange
        var policy = CreatePolicy(organizationId, NonExemptingPolicyType);
        policy.Enabled = false;
        var sut = new PreAccessPolicyEnforcer(Policies(policy), [], _factories);

        // Act
        var decision = sut.Evaluate(NonExemptingPolicyType, userId, OrganizationUserType.User);

        // Assert
        Assert.False(decision.IsEnforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_ExemptRole_ReturnsNotEnforced(Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(Policies(CreatePolicy(organizationId, ExemptingPolicyType)), [], _factories);

        // Act
        var decision = sut.Evaluate(ExemptingPolicyType, userId, OrganizationUserType.Custom);

        // Assert
        Assert.False(decision.IsEnforced);
    }

    [Theory]
    [BitAutoData(ExemptingPolicyType, OrganizationUserType.Owner)]
    [BitAutoData(ExemptingPolicyType, OrganizationUserType.Admin)]
    [BitAutoData(NonExemptingPolicyType, OrganizationUserType.Custom)]
    public void Evaluate_NonExemptRole_ReturnsEnforced(
        PolicyType policyType, OrganizationUserType proposedRole, Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(Policies(CreatePolicy(organizationId, policyType)), [], _factories);

        // Act
        var decision = sut.Evaluate(policyType, userId, proposedRole);

        // Assert
        Assert.True(decision.IsEnforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_ProviderUser_ExemptPolicy_ReturnsNotEnforced(Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(
            Policies(CreatePolicy(organizationId, ExemptingPolicyType)), [userId], _factories);

        // Act
        var decision = sut.Evaluate(ExemptingPolicyType, userId, OrganizationUserType.User);

        // Assert
        Assert.False(decision.IsEnforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_ProviderUser_NonExemptPolicy_ReturnsEnforced(Guid organizationId, Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(
            Policies(CreatePolicy(organizationId, NonExemptingPolicyType)), [userId], _factories);

        // Act
        var decision = sut.Evaluate(NonExemptingPolicyType, userId, OrganizationUserType.User);

        // Assert
        Assert.True(decision.IsEnforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_OtherUserIsProvider_ReturnsEnforced(Guid organizationId, Guid userId, Guid providerUserId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(
            Policies(CreatePolicy(organizationId, ExemptingPolicyType)), [providerUserId], _factories);

        // Act
        var decision = sut.Evaluate(ExemptingPolicyType, userId, OrganizationUserType.User);

        // Assert
        Assert.True(decision.IsEnforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_Enforced_ReturnsPolicyData(Guid organizationId, Guid userId)
    {
        // Arrange
        var policy = CreatePolicy(organizationId, NonExemptingPolicyType);
        policy.Data = CoreHelpers.ClassToJsonData(new TestPolicyDataModel { Setting = true });
        var sut = new PreAccessPolicyEnforcer(Policies(policy), [], _factories);

        // Act
        var decision = sut.Evaluate(NonExemptingPolicyType, userId, OrganizationUserType.User);

        // Assert
        Assert.True(decision.IsEnforced);
        Assert.True(decision.GetDataModel<TestPolicyDataModel>().Setting);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public void Evaluate_DelegatesToFactoryEnforcePreAccess(bool isProvider, Guid organizationId, Guid userId)
    {
        // Arrange
        const PolicyType policyType = PolicyType.SingleOrg;
        const OrganizationUserType proposedRole = OrganizationUserType.Custom;
        const string policyData = "{\"some\":\"data\"}";

        var policy = CreatePolicy(organizationId, policyType);
        policy.Data = policyData;
        var factory = Substitute.For<IPolicyRequirementFactory<IPolicyRequirement>, IPreAccessPolicyRequirementFactory>();
        var preAccessFactory = (IPreAccessPolicyRequirementFactory)factory;
        preAccessFactory.PolicyType.Returns(policyType);
        preAccessFactory.EnforcePreAccess(proposedRole, isProvider).Returns(true);
        var sut = new PreAccessPolicyEnforcer(Policies(policy), isProvider ? [userId] : [], [factory]);

        // Act
        var decision = sut.Evaluate(policyType, userId, proposedRole);

        // Assert
        preAccessFactory.Received(1).EnforcePreAccess(proposedRole, isProvider);
        Assert.True(decision.IsEnforced);
        Assert.Equal(policyData, decision.Data);
    }

    [Theory, BitAutoData]
    public void Evaluate_FactoryDoesNotSupportPreAccess_Throws(Guid organizationId, Guid userId)
    {
        // Arrange
        var factory = Substitute.For<IPolicyRequirementFactory<IPolicyRequirement>>();
        factory.PolicyType.Returns(PolicyType.SingleOrg);
        var sut = new PreAccessPolicyEnforcer(
            Policies(CreatePolicy(organizationId, PolicyType.SingleOrg)), [], [factory]);

        // Act & Assert
        var exception = Assert.Throws<NotImplementedException>(
            () => sut.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.User));
        Assert.Contains("No Requirement Factory found", exception.Message);
    }

    [Theory, BitAutoData]
    public void Evaluate_CanEvaluateManyUsersAndPolicies(Guid organizationId, Guid userId, Guid providerUserId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(
            Policies(
                CreatePolicy(organizationId, ExemptingPolicyType),
                CreatePolicy(organizationId, NonExemptingPolicyType)),
            [providerUserId], _factories);

        // Act & Assert
        Assert.True(sut.Evaluate(ExemptingPolicyType, userId, OrganizationUserType.User).IsEnforced);
        Assert.False(sut.Evaluate(ExemptingPolicyType, providerUserId, OrganizationUserType.User).IsEnforced);
        Assert.False(sut.Evaluate(ExemptingPolicyType, userId, OrganizationUserType.Custom).IsEnforced);
        Assert.True(sut.Evaluate(NonExemptingPolicyType, providerUserId, OrganizationUserType.Custom).IsEnforced);
    }

    [Theory, BitAutoData]
    public void Evaluate_NoFactoryRegistered_Throws(Guid userId)
    {
        // Arrange
        var sut = new PreAccessPolicyEnforcer(NoPolicies, [], []);

        // Act & Assert
        var exception = Assert.Throws<NotImplementedException>(
            () => sut.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.User));
        Assert.Contains("No Requirement Factory found", exception.Message);
    }

    private static readonly Policy[] NoPolicies = [];

    private static Policy[] Policies(params Policy[] policies) => policies;

    private static Policy CreatePolicy(Guid organizationId, PolicyType type) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Type = type, Enabled = true };

    private class TestPreAccessPolicyRequirementFactory(
        PolicyType policyType,
        IEnumerable<OrganizationUserType> exemptRoles,
        bool exemptProviders) : BasePolicyRequirementFactory<TestPolicyRequirement>
    {
        public override PolicyType PolicyType => policyType;
        protected override IEnumerable<OrganizationUserType> ExemptRoles => exemptRoles;
        protected override bool ExemptProviders => exemptProviders;

        public override TestPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
            => new() { Policies = policyDetails };
    }

    private class TestPolicyDataModel : IPolicyDataModel
    {
        public bool Setting { get; set; }
    }
}
