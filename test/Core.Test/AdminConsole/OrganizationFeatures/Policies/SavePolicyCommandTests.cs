#nullable enable

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
using Microsoft.Extensions.Time.Testing;
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

        var creationDate = sutProvider.GetDependency<FakeTimeProvider>().Start;

        await sutProvider.Sut.SaveAsync(policyUpdate);

        await fakePolicyValidator.ValidateAsyncMock.Received(1).Invoke(policyUpdate, null);
        fakePolicyValidator.OnSaveSideEffectsAsyncMock.Received(1).Invoke(policyUpdate, null);

        await AssertPolicySavedAsync(sutProvider, policyUpdate);
        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(Arg.Is<Policy>(p =>
            p.CreationDate == creationDate &&
            p.RevisionDate == creationDate));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ExistingPolicy_Success(
        [PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy currentPolicy)
    {
        var fakePolicyValidator = new FakeSingleOrgPolicyValidator();
        fakePolicyValidator.ValidateAsyncMock(policyUpdate, null).Returns("");
        var sutProvider = SutProviderFactory([fakePolicyValidator]);

        currentPolicy.OrganizationId = policyUpdate.OrganizationId;
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, policyUpdate.Type)
            .Returns(currentPolicy);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([currentPolicy]);

        // Store mutable properties separately to assert later
        var id = currentPolicy.Id;
        var organizationId = currentPolicy.OrganizationId;
        var type = currentPolicy.Type;
        var creationDate = currentPolicy.CreationDate;
        var revisionDate = sutProvider.GetDependency<FakeTimeProvider>().Start;

        await sutProvider.Sut.SaveAsync(policyUpdate);

        await fakePolicyValidator.ValidateAsyncMock.Received(1).Invoke(policyUpdate, currentPolicy);
        fakePolicyValidator.OnSaveSideEffectsAsyncMock.Received(1).Invoke(policyUpdate, currentPolicy);

        await AssertPolicySavedAsync(sutProvider, policyUpdate);
        // Additional assertions to ensure certain properties have or have not been updated
        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(Arg.Is<Policy>(p =>
            p.Id == id &&
            p.OrganizationId == organizationId &&
            p.Type == type &&
            p.CreationDate == creationDate &&
            p.RevisionDate == revisionDate));
    }

    [Fact]
    public void Constructor_DuplicatePolicyValidators_Throws()
    {
        var exception = Assert.Throws<Exception>(() =>
            new SavePolicyCommand(
                Substitute.For<IApplicationCacheService>(),
                Substitute.For<IEventService>(),
                Substitute.For<IPolicyRepository>(),
                [new FakeSingleOrgPolicyValidator(), new FakeSingleOrgPolicyValidator()],
                Substitute.For<TimeProvider>()
            ));
        Assert.Contains("Duplicate PolicyValidator for SingleOrg policy", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationDoesNotExist_ThrowsBadRequest([PolicyUpdate(PolicyType.ActivateAutofill)] PolicyUpdate policyUpdate)
    {
        var sutProvider = SutProviderFactory();
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policyUpdate.OrganizationId)
            .Returns(Task.FromResult<OrganizationAbility?>(null));

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policyUpdate));

        Assert.Contains("Organization not found", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationCannotUsePolicies_ThrowsBadRequest([PolicyUpdate(PolicyType.ActivateAutofill)] PolicyUpdate policyUpdate)
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
            () => sutProvider.Sut.SaveAsync(policyUpdate));

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
            () => sutProvider.Sut.SaveAsync(policyUpdate));

        Assert.Contains("Turn on the Single organization policy because it is required for the Require single sign-on authentication policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
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
            () => sutProvider.Sut.SaveAsync(policyUpdate));

        Assert.Contains("Turn on the Single organization policy because it is required for the Require single sign-on authentication policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
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

        await sutProvider.Sut.SaveAsync(policyUpdate);
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
            () => sutProvider.Sut.SaveAsync(policyUpdate));

        Assert.Contains("Turn off the Require single sign-on authentication policy because it requires the Single organization policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_MultipleDependentPoliciesAreEnabled_Throws(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso)] Policy requireSsoPolicy, // depends on Single Org
        [Policy(PolicyType.MaximumVaultTimeout)] Policy vaultTimeoutPolicy) // depends on Single Org
    {
        var sutProvider = SutProviderFactory([
            new FakeRequireSsoPolicyValidator(),
            new FakeSingleOrgPolicyValidator(),
            new FakeVaultTimeoutPolicyValidator()
        ]);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy, vaultTimeoutPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policyUpdate));

        Assert.Contains("Turn off all of the policies that require the Single organization policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
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

        await sutProvider.Sut.SaveAsync(policyUpdate);

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
            () => sutProvider.Sut.SaveAsync(policyUpdate));

        Assert.Contains("Validation error!", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    /// <summary>
    /// Returns a new SutProvider with the PolicyValidators registered in the Sut.
    /// </summary>
    private static SutProvider<SavePolicyCommand> SutProviderFactory(IEnumerable<IPolicyValidator>? policyValidators = null)
    {
        return new SutProvider<SavePolicyCommand>()
            .WithFakeTimeProvider()
            .SetDependency(typeof(IEnumerable<IPolicyValidator>), policyValidators ?? [])
            .Create();
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

    private static async Task AssertPolicySavedAsync(SutProvider<SavePolicyCommand> sutProvider, PolicyUpdate policyUpdate)
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
