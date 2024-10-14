#nullable enable

using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
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
    public async Task SaveAsync_NewPolicy_Success([PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate)
    {
        var fakePolicyValidator = new FakeSingleOrgPolicyValidator();
        fakePolicyValidator.ValidateAsyncMock(policyUpdate, null).Returns("");
        var sutProvider = SutProviderFactory([fakePolicyValidator]);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policyUpdate.OrganizationId).Returns([]);

        await sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid());

        fakePolicyValidator.OnSaveSideEffectsAsyncMock.Received(1).Invoke(policyUpdate, null, Arg.Any<IOrganizationService>());

        await AssertPolicySavedAsync(sutProvider, policyUpdate);
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
    public async Task SaveAsync_OrganizationDoesNotExist_ThrowsBadRequest(PolicyUpdate policyUpdate)
    {
        var sutProvider = SutProviderFactory();
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policyUpdate.OrganizationId)
            .Returns(Task.FromResult<OrganizationAbility?>(null));

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("Organization not found", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationCannotUsePolicies_ThrowsBadRequest(PolicyUpdate policyUpdate)
    {
        var sutProvider = SutProviderFactory();
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policyUpdate.OrganizationId)
            .Returns(new OrganizationAbility
            {
                Id = policyUpdate.OrganizationId,
                UsePolicies = false
            });

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("cannot use policies", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyIsNull_Throws(
        [PolicyUpdate(PolicyType.RequireSso)] PolicyUpdate policyUpdate)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("Policy requires PolicyType SingleOrg to be enabled", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyNotEnabled_Throws(
        [PolicyUpdate(PolicyType.RequireSso)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy singleOrgPolicy)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([singleOrgPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("Policy requires PolicyType SingleOrg to be enabled", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyEnabled_Success(
        [PolicyUpdate(PolicyType.RequireSso)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy)
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([singleOrgPolicy]);

        await sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid());
        await AssertPolicySavedAsync(sutProvider, policyUpdate);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DependentPolicyIsEnabled_Throws(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso)] Policy requireSsoPolicy) // depends on Single Org
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

        Assert.Contains("This policy is required by RequireSso policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DependentPolicyNotEnabled_Success(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso, false)] Policy requireSsoPolicy) // depends on Single Org but is not enabled
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy]);

        await sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid());

        await AssertPolicySavedAsync(sutProvider, policyUpdate);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ThrowsOnValidationError([PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate)
    {
        var fakePolicyValidator = new FakeSingleOrgPolicyValidator();
        fakePolicyValidator.ValidateAsyncMock(policyUpdate, null).Returns("Validation error!");
        var sutProvider = SutProviderFactory([fakePolicyValidator]);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policyUpdate.OrganizationId).Returns([]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policyUpdate, Substitute.For<IOrganizationService>(), Guid.NewGuid()));

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

    private static void ArrangeOrganization(SutProvider<SavePolicyCommand> sutProvider, PolicyUpdate policyUpdate)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policyUpdate.OrganizationId)
            .Returns(new OrganizationAbility
            {
                Id = policyUpdate.OrganizationId,
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

    private static async Task AssertPolicySavedAsync(SutProvider<SavePolicyCommand> sutProvider, PolicyUpdate policyUpdate,
        Guid? savingUserId = null)
    {
        var expectedPolicy = () => Arg.Is<Policy>(p =>
            p.Type == policyUpdate.Type &&
            p.OrganizationId == policyUpdate.OrganizationId &&
            p.Enabled == policyUpdate.Enabled &&
            p.Data == policyUpdate.Data);

        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(expectedPolicy());

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogPolicyEventAsync(expectedPolicy(), EventType.Policy_Updated);
    }
}
