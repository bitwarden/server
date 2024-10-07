#nullable enable

using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Xunit;
using EventType = Bit.Core.Enums.EventType;

namespace Bit.Core.Test.AdminConsole.Services;

public class PolicyServicevNextTests
{
    [Theory, BitAutoData]
    public async Task SaveAsync_Success([Policy(PolicyType.SingleOrg)] Policy policy)
    {
        var fakePolicyDefinition = new FakeSingleOrgPolicyDefinition();
        fakePolicyDefinition.ValidateAsyncMock(null, policy).Returns((string)null);
        var sutProvider = SutProviderFactory([fakePolicyDefinition]);

        var originalRevisionDate = policy.RevisionDate;

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policy.OrganizationId).Returns([]);

        await sutProvider.Sut.SaveAsync(policy,
            Substitute.For<IUserService>(),
            Substitute.For<IOrganizationService>(),
            Guid.NewGuid());

        fakePolicyDefinition.OnSaveSideEffectsAsyncMock.Received(1).Invoke(null, policy);
        Assert.NotEqual(originalRevisionDate, policy.RevisionDate);
        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(policy);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogPolicyEventAsync(policy, EventType.Policy_Updated);
    }

    [Fact]
    public void Constructor_DuplicatePolicyDefinitions_Throws()
    {
        var exception = Assert.Throws<Exception>(() =>
            new PolicyServicevNext(
                Substitute.For<IApplicationCacheService>(),
                Substitute.For<IEventService>(),
                Substitute.For<IPolicyRepository>(),
                [new FakeSingleOrgPolicyDefinition(), new FakeSingleOrgPolicyDefinition()]
            ));
        Assert.Contains("Duplicate PolicyDefinition for SingleOrg policy", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationDoesNotExist_ThrowsBadRequest(Policy policy)
    {
        var sutProvider = SutProviderFactory();
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policy.OrganizationId)
            .Returns((OrganizationAbility)null);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Substitute.For<IUserService>(),
                Substitute.For<IOrganizationService>(),
                Guid.NewGuid()));

        Assert.Contains("Organization not found", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationCannotUsePolicies_ThrowsBadRequest(Policy policy)
    {
        var sutProvider = SutProviderFactory();
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policy.OrganizationId)
            .Returns(new OrganizationAbility
            {
                Id = policy.OrganizationId,
                UsePolicies = false
            });

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Substitute.For<IUserService>(),
                Substitute.For<IOrganizationService>(),
                Guid.NewGuid()));

        Assert.Contains("cannot use policies", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_PolicyDefinitionNotFound_Throws([Policy(PolicyType.SingleOrg)]Policy policy)
    {
        var sutProvider = SutProviderFactory();
        ArrangeOrganization(sutProvider, policy);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.SaveAsync(policy,
                Substitute.For<IUserService>(),
                Substitute.For<IOrganizationService>(),
                Guid.NewGuid()));

        Assert.Contains("No PolicyDefinition found for SingleOrg policy", exception.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyIsNull_Throws(
        [Policy(PolicyType.RequireSso)] Policy policy)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyDefinition(),
            new FakeSingleOrgPolicyDefinition()
        ]);

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Substitute.For<IUserService>(),
                Substitute.For<IOrganizationService>(),
                Guid.NewGuid()));

        Assert.Contains("Policy requires PolicyType SingleOrg to be enabled", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyNotEnabled_Throws(
        [Policy(PolicyType.SingleOrg, false)] Policy singleOrgPolicy,
        [Policy(PolicyType.RequireSso)] Policy policy)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyDefinition(),
            new FakeSingleOrgPolicyDefinition()
        ]);

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([singleOrgPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Substitute.For<IUserService>(),
                Substitute.For<IOrganizationService>(),
                Guid.NewGuid()));

        Assert.Contains("Policy requires PolicyType SingleOrg to be enabled", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyEnabled_Success(
        [Policy(PolicyType.SingleOrg, true)] Policy singleOrgPolicy,
        [Policy(PolicyType.RequireSso)] Policy policy)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyDefinition(),
            new FakeSingleOrgPolicyDefinition()
        ]);

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([singleOrgPolicy]);

        await sutProvider.Sut.SaveAsync(policy,
            Substitute.For<IUserService>(),
            Substitute.For<IOrganizationService>(),
            Guid.NewGuid());

        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(policy);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DependentPolicyIsEnabled_Throws(
        [Policy(PolicyType.SingleOrg, false)] Policy policy,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso)] Policy requireSsoPolicy) // depends on Single Org
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyDefinition(),
            new FakeSingleOrgPolicyDefinition()
        ]);

        policy.Id = currentPolicy.Id;
        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Substitute.For<IUserService>(),
                Substitute.For<IOrganizationService>(),
                Guid.NewGuid()));

        Assert.Contains("This policy is required by RequireSso policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DependentPolicyNotEnabled_Success(
        [Policy(PolicyType.SingleOrg, false)] Policy policy,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso, false)] Policy requireSsoPolicy) // depends on Single Org but is not enabled
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyDefinition(),
            new FakeSingleOrgPolicyDefinition()
        ]);

        policy.Id = currentPolicy.Id;
        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy]);

        await sutProvider.Sut.SaveAsync(policy,
            Substitute.For<IUserService>(),
            Substitute.For<IOrganizationService>(),
            Guid.NewGuid());

        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(policy);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ThrowsOnValidationError([Policy(PolicyType.SingleOrg)] Policy policy)
    {
        var fakePolicyDefinition = new FakeSingleOrgPolicyDefinition();
        fakePolicyDefinition.ValidateAsyncMock(null, policy).Returns("Validation error!");
        var sutProvider = SutProviderFactory([fakePolicyDefinition]);

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policy.OrganizationId).Returns([]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Substitute.For<IUserService>(),
                Substitute.For<IOrganizationService>(),
                Guid.NewGuid()));

        Assert.Contains("Validation error!", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    /// <summary>
    /// Returns a new SutProvider with the PolicyDefinitions registered in the Sut.
    /// </summary>
    private static SutProvider<PolicyServicevNext> SutProviderFactory(IEnumerable<IPolicyDefinition> policyDefinitions = null)
    {
        var fixture = new Fixture();
        fixture.Customizations.Add(new PolicyServicevNextBuilder(policyDefinitions ?? new List<IPolicyDefinition>()));
        var sutProvider = new SutProvider<PolicyServicevNext>(fixture);
        sutProvider.Create();
        return sutProvider;
    }

    private static void ArrangeOrganization(SutProvider<PolicyServicevNext> sutProvider, Policy policy)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policy.OrganizationId)
            .Returns(new OrganizationAbility
            {
                Id = policy.OrganizationId,
                UsePolicies = true
            });
    }

    private static async Task AssertPolicyNotSavedAsync(SutProvider<PolicyServicevNext> sutProvider)
    {
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }
}
