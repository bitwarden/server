using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
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
        var thisPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        var otherPolicy = new PolicyDetails { PolicyType = PolicyType.RequireSso };
        var policyRepository = Substitute.For<IPolicyRepository>();
        policyRepository.GetPolicyDetailsByUserId(userId).Returns([otherPolicy, thisPolicy]);

        var factory = new TestPolicyRequirementFactory(_ => true);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetAsync<TestPolicyRequirement>(userId);

        Assert.Contains(thisPolicy, requirement.Policies);
        Assert.DoesNotContain(otherPolicy, requirement.Policies);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_CallsEnforceCallback(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var thisPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        var otherPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        policyRepository.GetPolicyDetailsByUserId(userId).Returns([thisPolicy, otherPolicy]);

        var factory = new TestPolicyRequirementFactory(x => x == thisPolicy);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

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
        policyRepository.GetPolicyDetailsByUserId(userId).Returns([]);

        var factory = new TestPolicyRequirementFactory(x => x.IsProvider);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetAsync<TestPolicyRequirement>(userId);

        Assert.Empty(requirement.Policies);
    }
}
