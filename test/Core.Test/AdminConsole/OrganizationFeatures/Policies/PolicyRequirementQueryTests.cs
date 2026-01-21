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
    public async Task GetAsync_WithMultipleUserIds_ReturnsRequirementPerUser(Guid userIdA, Guid userIdB)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var policyA = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = userIdA };
        var policyB = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = userIdB };
        policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
                Arg.Any<IEnumerable<Guid>>(), PolicyType.SingleOrg)
            .Returns([policyA, policyB]);

        var factory = new TestPolicyRequirementFactory(_ => true);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirements = (await sut.GetAsync<TestPolicyRequirement>([userIdA, userIdB])).ToList();

        Assert.Equal(2, requirements.Count);
        Assert.Contains(policyA, requirements[0].Policies);
        Assert.DoesNotContain(policyB, requirements[0].Policies);
        Assert.Contains(policyB, requirements[1].Policies);
        Assert.DoesNotContain(policyA, requirements[1].Policies);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_WithMultipleUserIds_CallsEnforceCallback(Guid userIdA, Guid userIdB)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var policyA = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = userIdA };
        var policyB = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = userIdB };
        policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
                Arg.Any<IEnumerable<Guid>>(), PolicyType.SingleOrg)
            .Returns([policyA, policyB]);

        var callback = Substitute.For<Func<PolicyDetails, bool>>();
        callback(Arg.Any<PolicyDetails>()).Returns(x => x.Arg<PolicyDetails>() == policyA);

        var factory = new TestPolicyRequirementFactory(callback);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirements = (await sut.GetAsync<TestPolicyRequirement>([userIdA, userIdB])).ToList();

        Assert.Contains(policyA, requirements[0].Policies);
        Assert.Empty(requirements[1].Policies);
        callback.Received()(Arg.Is(policyA));
        callback.Received()(Arg.Is(policyB));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_WithMultipleUserIds_FiltersOutPoliciesThatAreNotEnforced(Guid userIdA, Guid userIdB)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var enforcedPolicyA = new OrganizationPolicyDetails
        { PolicyType = PolicyType.SingleOrg, UserId = userIdA, IsProvider = false };
        var notEnforcedPolicyA = new OrganizationPolicyDetails
        { PolicyType = PolicyType.SingleOrg, UserId = userIdA, IsProvider = true };
        var enforcedPolicyB = new OrganizationPolicyDetails
        { PolicyType = PolicyType.SingleOrg, UserId = userIdB, IsProvider = false };
        policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
                Arg.Any<IEnumerable<Guid>>(), PolicyType.SingleOrg)
            .Returns([enforcedPolicyA, notEnforcedPolicyA, enforcedPolicyB]);

        // Enforce returns false for providers (filtering them out)
        var factory = new TestPolicyRequirementFactory(p => !p.IsProvider);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirements = (await sut.GetAsync<TestPolicyRequirement>([userIdA, userIdB])).ToList();

        Assert.Equal(2, requirements.Count);
        Assert.Contains(enforcedPolicyA, requirements[0].Policies);
        Assert.DoesNotContain(notEnforcedPolicyA, requirements[0].Policies);
        Assert.Contains(enforcedPolicyB, requirements[1].Policies);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_WithMultipleUserIds_ThrowsIfNoFactoryRegistered(Guid userIdA, Guid userIdB)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var sut = new PolicyRequirementQuery(policyRepository, []);

        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sut.GetAsync<TestPolicyRequirement>([userIdA, userIdB]));
        Assert.Contains("No Requirement Factory found", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_WithMultipleUserIds_HandlesNoPolicies(Guid userIdA, Guid userIdB)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
                Arg.Any<IEnumerable<Guid>>(), PolicyType.SingleOrg)
            .Returns([]);

        var factory = new TestPolicyRequirementFactory(_ => true);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirements = (await sut.GetAsync<TestPolicyRequirement>([userIdA, userIdB])).ToList();

        Assert.Equal(2, requirements.Count);
        Assert.Empty(requirements[0].Policies);
        Assert.Empty(requirements[1].Policies);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_WithMultipleUserIds_ReturnsEmptyRequirementForUserWithoutPolicies(
        Guid userIdA, Guid userIdB)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var policyA = new OrganizationPolicyDetails { PolicyType = PolicyType.SingleOrg, UserId = userIdA };
        // Only userIdA has a policy, userIdB has none
        policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
                Arg.Any<IEnumerable<Guid>>(), PolicyType.SingleOrg)
            .Returns([policyA]);

        var factory = new TestPolicyRequirementFactory(_ => true);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirements = (await sut.GetAsync<TestPolicyRequirement>([userIdA, userIdB])).ToList();

        Assert.Equal(2, requirements.Count);
        Assert.Contains(policyA, requirements[0].Policies);
        Assert.Empty(requirements[1].Policies);
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
