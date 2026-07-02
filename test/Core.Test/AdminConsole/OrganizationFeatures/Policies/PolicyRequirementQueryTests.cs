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
    public async Task GetAsyncVNext_CallsEnforceCallback(Guid userId)
    {
        // Arrange policies
        var policyRepository = Substitute.For<IPolicyRepository>();
        var thisPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        var otherPolicy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(userId, PolicyType.SingleOrg)
            .Returns([thisPolicy, otherPolicy]);

        // Arrange a substitute Enforce function so that we can inspect the received calls
        var callback = Substitute.For<Func<PolicyDetails, bool>>();
        callback(Arg.Any<PolicyDetails>()).Returns(x => x.Arg<PolicyDetails>() == thisPolicy);

        // Arrange the sut
        var factory = new TestPolicyRequirementFactory(callback);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        // Act
        var requirement = await sut.GetAsyncVNext<TestPolicyRequirement>(userId);

        // Assert
        Assert.Contains(thisPolicy, requirement.Policies);
        Assert.DoesNotContain(otherPolicy, requirement.Policies);
        callback.Received()(Arg.Is(thisPolicy));
        callback.Received()(Arg.Is(otherPolicy));
    }

    [Theory, BitAutoData]
    public async Task GetAsyncVNext_ThrowsIfNoFactoryRegistered(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var sut = new PolicyRequirementQuery(policyRepository, []);

        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sut.GetAsyncVNext<TestPolicyRequirement>(userId));
        Assert.Contains("No Requirement Factory found", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetAsyncVNext_HandlesNoPolicies(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(userId, PolicyType.SingleOrg)
            .Returns([]);

        var factory = new TestPolicyRequirementFactory(x => x.IsProvider);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetAsyncVNext<TestPolicyRequirement>(userId);

        Assert.Empty(requirement.Policies);
    }

    [Theory, BitAutoData]
    public async Task GetAsyncVNext_ForDefaultOnFactory_UsesWithStateQueryAndTreatsMissingAsEnabled(Guid userId)
    {
        // Arrange raw-state rows: an enabled org, a default (no row) org, and an explicitly disabled org
        var policyRepository = Substitute.For<IPolicyRepository>();
        var enabledOrg = new PolicyDetailsWithState { PolicyType = PolicyType.SingleOrg, Enabled = true };
        var defaultOrg = new PolicyDetailsWithState { PolicyType = PolicyType.SingleOrg, Enabled = null };
        var disabledOrg = new PolicyDetailsWithState { PolicyType = PolicyType.SingleOrg, Enabled = false };
        policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(userId, PolicyType.SingleOrg)
            .Returns([enabledOrg, defaultOrg, disabledOrg]);

        var factory = new TestPolicyRequirementFactory(_ => true, PolicyDefaultState.Enabled);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        // Act
        var requirement = await sut.GetAsyncVNext<TestPolicyRequirement>(userId);

        // Assert: enabled and default (missing) orgs are enforced; the disabled org is dropped
        Assert.Contains(enabledOrg, requirement.Policies);
        Assert.Contains(defaultOrg, requirement.Policies);
        Assert.DoesNotContain(disabledOrg, requirement.Policies);

        // The standard query must not be used for a default-on factory
        await policyRepository.Received(1)
            .GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(userId, PolicyType.SingleOrg);
        await policyRepository.DidNotReceive()
            .GetPolicyDetailsByUserIdAndPolicyTypeAsync(userId, Arg.Any<PolicyType>());
    }

    [Theory, BitAutoData]
    public async Task GetAsyncVNext_ForDefaultOnFactory_CallsEnforceCallback(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var thisPolicy = new PolicyDetailsWithState { PolicyType = PolicyType.SingleOrg, Enabled = null };
        var otherPolicy = new PolicyDetailsWithState { PolicyType = PolicyType.SingleOrg, Enabled = true };
        policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(userId, PolicyType.SingleOrg)
            .Returns([thisPolicy, otherPolicy]);

        var callback = Substitute.For<Func<PolicyDetails, bool>>();
        callback(Arg.Any<PolicyDetails>()).Returns(x => x.Arg<PolicyDetails>() == thisPolicy);

        var factory = new TestPolicyRequirementFactory(callback, PolicyDefaultState.Enabled);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetAsyncVNext<TestPolicyRequirement>(userId);

        Assert.Contains(thisPolicy, requirement.Policies);
        Assert.DoesNotContain(otherPolicy, requirement.Policies);
        callback.Received()(Arg.Is<PolicyDetails>(thisPolicy));
        callback.Received()(Arg.Is<PolicyDetails>(otherPolicy));
    }

    [Theory, BitAutoData]
    public async Task GetAsyncVNext_ForDefaultOffFactory_UsesStandardQuery(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var policy = new PolicyDetails { PolicyType = PolicyType.SingleOrg };
        policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(userId, PolicyType.SingleOrg)
            .Returns([policy]);

        var factory = new TestPolicyRequirementFactory(_ => true); // DefaultState.Disabled
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var requirement = await sut.GetAsyncVNext<TestPolicyRequirement>(userId);

        Assert.Contains(policy, requirement.Policies);
        await policyRepository.Received(1).GetPolicyDetailsByUserIdAndPolicyTypeAsync(userId, PolicyType.SingleOrg);
        await policyRepository.DidNotReceive()
            .GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(userId, Arg.Any<PolicyType>());
    }

    [Theory, BitAutoData]
    public async Task GetAsync_Batch_ForDefaultOnFactory_Throws(Guid userIdA, Guid userIdB)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var factory = new TestPolicyRequirementFactory(_ => true, PolicyDefaultState.Enabled);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sut.GetAsync<TestPolicyRequirement>([userIdA, userIdB]));
        Assert.Contains("GetAsyncVNext", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_SingleUser_ForDefaultOnFactory_Throws(Guid userId)
    {
        // The single-user legacy overload delegates to the batch overload, so it inherits the guard.
        var policyRepository = Substitute.For<IPolicyRepository>();
        var factory = new TestPolicyRequirementFactory(_ => true, PolicyDefaultState.Enabled);
        var sut = new PolicyRequirementQuery(policyRepository, [factory]);

        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sut.GetAsync<TestPolicyRequirement>(userId));
        Assert.Contains("GetAsyncVNext", exception.Message);
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
        Assert.Equal(userIdA, requirements[0].UserId);
        Assert.Equal(userIdB, requirements[1].UserId);
        Assert.Contains(policyA, requirements[0].Requirement.Policies);
        Assert.DoesNotContain(policyB, requirements[0].Requirement.Policies);
        Assert.Contains(policyB, requirements[1].Requirement.Policies);
        Assert.DoesNotContain(policyA, requirements[1].Requirement.Policies);
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

        Assert.Contains(policyA, requirements[0].Requirement.Policies);
        Assert.Empty(requirements[1].Requirement.Policies);
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
        Assert.Contains(enforcedPolicyA, requirements[0].Requirement.Policies);
        Assert.DoesNotContain(notEnforcedPolicyA, requirements[0].Requirement.Policies);
        Assert.Contains(enforcedPolicyB, requirements[1].Requirement.Policies);
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
        Assert.Equal(userIdA, requirements[0].UserId);
        Assert.Equal(userIdB, requirements[1].UserId);
        Assert.Empty(requirements[0].Requirement.Policies);
        Assert.Empty(requirements[1].Requirement.Policies);
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
        Assert.Equal(userIdA, requirements[0].UserId);
        Assert.Equal(userIdB, requirements[1].UserId);
        Assert.Contains(policyA, requirements[0].Requirement.Policies);
        Assert.Empty(requirements[1].Requirement.Policies);
    }
}
