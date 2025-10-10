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
        var thisPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = userId };
        var otherPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.RequireSso, UserId = userId };
        var policyRepository = Substitute.For<IPolicyRepository>();
        policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(userId)), PolicyType.SingleOrg)
            .Returns([otherPolicy, thisPolicy]);

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
        var thisPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = userId };
        var otherPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = userId };
        policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(userId)), PolicyType.SingleOrg)
            .Returns([thisPolicy, otherPolicy]);

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
        policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(userId)), PolicyType.SingleOrg)
            .Returns([]);

        var factory = new TestPolicyRequirementFactory(x => x.IsProvider);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetAsync<TestPolicyRequirement>(userId);

        Assert.Empty(requirement.Policies);
    }

    [Theory, BitAutoData]
    public async Task GetManyByOrganizationIdAsync_IgnoresOtherPolicyTypes(Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var thisPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, OrganizationUserId = Guid.NewGuid() };
        var otherPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.RequireSso, OrganizationUserId = Guid.NewGuid() };
        // Force the repository to return both policies even though that is not the expected result
        policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.SingleOrg)
            .Returns([thisPolicy, otherPolicy]);

        var factory = new TestPolicyRequirementFactory(_ => true);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var organizationUserIds = await sut.GetManyByOrganizationIdAsync<TestPolicyRequirement>(organizationId);

        await policyRepository.Received(1).GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.SingleOrg);

        Assert.Contains(thisPolicy.OrganizationUserId, organizationUserIds);
        Assert.DoesNotContain(otherPolicy.OrganizationUserId, organizationUserIds);
    }

    [Theory, BitAutoData]
    public async Task GetManyByOrganizationIdAsync_CallsEnforceCallback(Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var thisPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, OrganizationUserId = Guid.NewGuid() };
        var otherPolicy = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, OrganizationUserId = Guid.NewGuid() };
        policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.SingleOrg).Returns([thisPolicy, otherPolicy]);

        var callback = Substitute.For<Func<PolicyDetails, bool>>();
        callback(Arg.Any<PolicyDetails>()).Returns(x => x.Arg<PolicyDetails>() == thisPolicy);

        var factory = new TestPolicyRequirementFactory(callback);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var organizationUserIds = await sut.GetManyByOrganizationIdAsync<TestPolicyRequirement>(organizationId);

        Assert.Contains(thisPolicy.OrganizationUserId, organizationUserIds);
        Assert.DoesNotContain(otherPolicy.OrganizationUserId, organizationUserIds);
        callback.Received()(Arg.Is<PolicyDetails>(p => p == thisPolicy));
        callback.Received()(Arg.Is<PolicyDetails>(p => p == otherPolicy));
    }

    [Theory, BitAutoData]
    public async Task GetManyByOrganizationIdAsync_ThrowsIfNoFactoryRegistered(Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var sut = new PolicyRequirementQuery(policyRepository, []);

        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sut.GetManyByOrganizationIdAsync<TestPolicyRequirement>(organizationId));

        Assert.Contains("No Requirement Factory found", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetManyByOrganizationIdAsync_HandlesNoPolicies(Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.SingleOrg).Returns([]);

        var factory = new TestPolicyRequirementFactory(x => x.IsProvider);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var organizationUserIds = await sut.GetManyByOrganizationIdAsync<TestPolicyRequirement>(organizationId);

        Assert.Empty(organizationUserIds);
    }
}
