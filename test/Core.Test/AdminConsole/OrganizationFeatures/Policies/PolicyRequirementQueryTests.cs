using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

[SutProviderCustomize]
public class PolicyRequirementQueryTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_IgnoresOtherPolicyTypes(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var otherPolicy = new PolicyDetails { PolicyType = PolicyType.RequireSso };
        var thisPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        var factory = new TestPolicyRequirementFactory(_ => true);

        var sut = new PolicyRequirementQuery(policyRepository, [factory]);
        policyRepository.GetPolicyDetailsByUserId(userId).Returns([otherPolicy, thisPolicy]);

        var requirement = await sut.GetAsync<TestPolicyRequirement>(userId);
        Assert.Contains(thisPolicy, requirement.Policies);
        Assert.DoesNotContain(otherPolicy, requirement.Policies);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_CallsEnforceCallback(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var otherPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        var thisPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };

        var factory = new TestPolicyRequirementFactory(x => x == thisPolicy);

        var sut = new PolicyRequirementQuery(policyRepository, [factory]);
        policyRepository.GetPolicyDetailsByUserId(userId).Returns([thisPolicy, otherPolicy]);

        var requirement = await sut.GetAsync<TestPolicyRequirement>(userId);

        Assert.Contains(thisPolicy, requirement.Policies);
        Assert.DoesNotContain(otherPolicy, requirement.Policies);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ThrowsIfNoFactoryRegistered(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var sut = new PolicyRequirementQuery(policyRepository, []);

        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sut.GetAsync<TestPolicyRequirement>(userId));
        Assert.Contains("No Requirement Factory found", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_HandlesNoPolicies(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var factory = new TestPolicyRequirementFactory(x => x.IsProvider);

        var sut = new PolicyRequirementQuery(policyRepository, [factory]);
        policyRepository.GetPolicyDetailsByUserId(userId).Returns([]);

        var requirement = await sut.GetAsync<TestPolicyRequirement>(userId);
        Assert.Empty(requirement.Policies);
    }

    /// <summary>
    /// Intentionally simplified PolicyRequirement that just holds the input PolicyDetails for us to assert against.
    /// </summary>
    private class TestPolicyRequirement : IPolicyRequirement
    {
        public IEnumerable<PolicyDetails> Policies { get; set; } = [];
    }

    private class TestPolicyRequirementFactory(Func<PolicyDetails, bool> enforce) : IRequirementFactory<TestPolicyRequirement>
    {
        public PolicyType PolicyType => PolicyType.SingleOrg;

        public bool Enforce(PolicyDetails policyDetails) => enforce(policyDetails);

        public TestPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
            => new() { Policies = policyDetails };
    }
}
