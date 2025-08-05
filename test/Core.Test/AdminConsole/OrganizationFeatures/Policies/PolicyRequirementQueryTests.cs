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
        // Arrange policies
        var policyRepository = Substitute.For<IPolicyRepository>();
        var thisPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        var otherPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        policyRepository.GetPolicyDetailsByUserId(userId).Returns([thisPolicy, otherPolicy]);

        // Arrange a substitute Enforce function so that we can inspect the received calls
        var callback = Substitute.For<Func<PolicyDetails, bool>>();
        callback(Arg.Any<PolicyDetails>()).Returns(x => x.Arg<PolicyDetails>() == thisPolicy);

        // Arrange the sut
        var factory = new TestPolicyRequirementFactory(callback);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        // Act
        var requirement = await sut.GetAsync<TestPolicyRequirement>(userId);

        // Assert
        Assert.Contains(thisPolicy, requirement.Policies);
        Assert.DoesNotContain(otherPolicy, requirement.Policies);
        callback.Received()(Arg.Is(thisPolicy));
        callback.Received()(Arg.Is(otherPolicy));
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

    [Theory, BitAutoData]
    public async Task GetByOrganizationAsync_IgnoresOtherPolicyTypes(Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var thisPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = Guid.NewGuid() };
        var otherPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.RequireSso, UserId = Guid.NewGuid() };
        // Force the repository to return both policies even though that is not the expected result
        policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.SingleOrg)
            .Returns([thisPolicy, otherPolicy]);

        var factory = new TestPolicyRequirementFactory(_ => true);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetByOrganizationAsync<TestPolicyRequirement>(organizationId);

        await policyRepository.Received(1).GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.SingleOrg);

        Assert.Contains(thisPolicy, requirement.Policies.Cast<OrganizationPolicyDetails>());
        Assert.DoesNotContain(otherPolicy, requirement.Policies.Cast<OrganizationPolicyDetails>());
    }

    [Theory, BitAutoData]
    public async Task GetByOrganizationAsync_CallsEnforceCallback(Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var thisPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = Guid.NewGuid() };
        var otherPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = Guid.NewGuid() };
        policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.SingleOrg).Returns([thisPolicy, otherPolicy]);

        var callback = Substitute.For<Func<PolicyDetails, bool>>();
        callback(Arg.Any<PolicyDetails>()).Returns(x => x.Arg<PolicyDetails>() == thisPolicy);

        var factory = new TestPolicyRequirementFactory(callback);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetByOrganizationAsync<TestPolicyRequirement>(organizationId);

        Assert.Contains(thisPolicy, requirement.Policies.Cast<OrganizationPolicyDetails>());
        Assert.DoesNotContain(otherPolicy, requirement.Policies.Cast<OrganizationPolicyDetails>());
        callback.Received()(Arg.Is<PolicyDetails>(p => p == thisPolicy));
        callback.Received()(Arg.Is<PolicyDetails>(p => p == otherPolicy));
    }

    [Theory, BitAutoData]
    public async Task GetByOrganizationAsync_ThrowsIfNoFactoryRegistered(Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var sut = new PolicyRequirementQuery(policyRepository, []);

        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sut.GetByOrganizationAsync<TestPolicyRequirement>(organizationId));

        Assert.Contains("No Requirement Factory found", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetByOrganizationAsync_HandlesNoPolicies(Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.SingleOrg).Returns([]);

        var factory = new TestPolicyRequirementFactory(x => x.IsProvider);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetByOrganizationAsync<TestPolicyRequirement>(organizationId);

        Assert.Empty(requirement.Policies);
    }
}
