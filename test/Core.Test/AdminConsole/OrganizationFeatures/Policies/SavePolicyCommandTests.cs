#nullable enable

using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using EventType = Bit.Core.Enums.EventType;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

public class SavePolicyCommandTests
{
    [Theory, BitAutoData]
    public async Task SaveAsync_Success([Policy(PolicyType.SingleOrg)] Policy policy)
    {
        var fakePolicyValidator = new FakeSingleOrgPolicyValidator();
        fakePolicyValidator.ValidateAsyncMock(null, policy).Returns("");
        var sutProvider = SutProviderFactory([fakePolicyValidator]);

        var originalRevisionDate = policy.RevisionDate;

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policy.OrganizationId).Returns([]);

        await sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid());

        fakePolicyValidator.OnSaveSideEffectsAsyncMock.Received(1).Invoke(null, policy, Arg.Any<IOrganizationService>());
        Assert.NotEqual(originalRevisionDate, policy.RevisionDate);
        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(policy);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogPolicyEventAsync(policy, EventType.Policy_Updated);
    }

    [Fact]
    public void Constructor_DuplicatePolicyValidators_Throws()
    {
        var exception = Assert.Throws<Exception>(() =>
            new SavePolicyCommand(
                Substitute.For<IApplicationCacheService>(),
                Substitute.For<IEventService>(),
                Substitute.For<IPolicyRepository>(),
                [new FakeSingleOrgPolicyValidator(), new FakeSingleOrgPolicyValidator()]
            ));
        Assert.Contains("Duplicate PolicyValidator for SingleOrg policy", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationDoesNotExist_ThrowsBadRequest(Policy policy)
    {
        var sutProvider = SutProviderFactory();
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policy.OrganizationId)
            .Returns(Task.FromResult<OrganizationAbility?>(null));

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

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
            () => sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("cannot use policies", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyIsNull_Throws(
        [Policy(PolicyType.RequireSso)] Policy policy)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("Policy requires PolicyType SingleOrg to be enabled", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyNotEnabled_Throws(
        [Policy(PolicyType.SingleOrg, false)] Policy singleOrgPolicy,
        [Policy(PolicyType.RequireSso)] Policy policy)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([singleOrgPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("Policy requires PolicyType SingleOrg to be enabled", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyEnabled_Success(
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        [Policy(PolicyType.RequireSso)] Policy policy)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([singleOrgPolicy]);

        await sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid());

        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(policy);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DependentPolicyIsEnabled_Throws(
        [Policy(PolicyType.SingleOrg, false)] Policy policy,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso)] Policy requireSsoPolicy) // depends on Single Org
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        policy.Id = currentPolicy.Id;
        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

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
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        policy.Id = currentPolicy.Id;
        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy]);

        await sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid());

        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(policy);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ThrowsOnValidationError([Policy(PolicyType.SingleOrg)] Policy policy)
    {
        var fakePolicyValidator = new FakeSingleOrgPolicyValidator();
        fakePolicyValidator.ValidateAsyncMock(null, policy).Returns("Validation error!");
        var sutProvider = SutProviderFactory([fakePolicyValidator]);

        ArrangeOrganization(sutProvider, policy);
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policy.OrganizationId).Returns([]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("Validation error!", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    /// <summary>
    /// Returns a new SutProvider with the PolicyValidators registered in the Sut.
    /// </summary>
    private static SutProvider<SavePolicyCommand> SutProviderFactory(IEnumerable<IPolicyValidator>? policyValidators = null)
    {
        var fixture = new Fixture();
        fixture.Customizations.Add(new SavePolicyCommandSpecimenBuilder(policyValidators ?? new List<IPolicyValidator>()));
        var sutProvider = new SutProvider<SavePolicyCommand>(fixture);
        sutProvider.Create();
        return sutProvider;
    }

    private static void ArrangeOrganization(SutProvider<SavePolicyCommand> sutProvider, Policy policy)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policy.OrganizationId)
            .Returns(new OrganizationAbility
            {
                Id = policy.OrganizationId,
                UsePolicies = true
            });
    }

    private static async Task AssertPolicyNotSavedAsync(SutProvider<SavePolicyCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default);
    }
}
