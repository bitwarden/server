#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OneOf.Types;
using Xunit;
using EventType = Bit.Core.Enums.EventType;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

public class VNextSavePolicyCommandTests
{
    [Theory, BitAutoData]
    // Jimmy
    public async Task SaveAsync_NewPolicy_Success([PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate)
    {
        // Arrange
        var fakePolicyValidationEvent = new FakeSingleOrgValidationEvent();
        fakePolicyValidationEvent.ValidateAsyncMock(Arg.Any<SavePolicyModel>(), Arg.Any<Policy>()).Returns("");
        var sutProvider = SutProviderFactory(
            [new FakeSingleOrgDependencyEvent()],
            [fakePolicyValidationEvent]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        var newPolicy = new Policy
        {
            Type = policyUpdate.Type,
            OrganizationId = policyUpdate.OrganizationId,
            Enabled = false
        };

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policyUpdate.OrganizationId).Returns([newPolicy]);

        var creationDate = sutProvider.GetDependency<FakeTimeProvider>().Start;

        // Act
        await sutProvider.Sut.SaveAsync(savePolicyModel);

        // Assert
        await fakePolicyValidationEvent.ValidateAsyncMock
            .Received(1)
            .Invoke(Arg.Any<SavePolicyModel>(), Arg.Any<Policy>());

        await AssertPolicySavedAsync(sutProvider, policyUpdate);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.CreationDate == creationDate &&
                p.RevisionDate == creationDate));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ExistingPolicy_Success(
        [PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy currentPolicy)
    {
        // Arrange
        var fakePolicyValidationEvent = new FakeSingleOrgValidationEvent();
        fakePolicyValidationEvent.ValidateAsyncMock(Arg.Any<SavePolicyModel>(), Arg.Any<Policy>()).Returns("");
        var sutProvider = SutProviderFactory(
            [new FakeSingleOrgDependencyEvent()],
            [fakePolicyValidationEvent]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        currentPolicy.OrganizationId = policyUpdate.OrganizationId;
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, policyUpdate.Type)
            .Returns(currentPolicy);

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([currentPolicy]);

        await sutProvider.Sut.SaveAsync(savePolicyModel);

        // Assert
        await fakePolicyValidationEvent.ValidateAsyncMock
            .Received(1)
            .Invoke(Arg.Any<SavePolicyModel>(), currentPolicy);

        await AssertPolicySavedAsync(sutProvider, policyUpdate);


        var revisionDate = sutProvider.GetDependency<FakeTimeProvider>().Start;

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Policy>(p =>
                p.Id == currentPolicy.Id &&
                p.OrganizationId == currentPolicy.OrganizationId &&
                p.Type == currentPolicy.Type &&
                p.CreationDate == currentPolicy.CreationDate &&
                p.RevisionDate == revisionDate));
    }

    [Fact]
    public void Constructor_DuplicatePolicyDependencyEvents_Throws()
    {
        var exception = Assert.Throws<Exception>(() =>
            new VNextSavePolicyCommand(
                Substitute.For<IApplicationCacheService>(),
                Substitute.For<IEventService>(),
                Substitute.For<IPolicyRepository>(),
                [new FakeSingleOrgDependencyEvent(), new FakeSingleOrgDependencyEvent()],
                Substitute.For<TimeProvider>(),
                Substitute.For<IPolicyEventHandlerFactory>()));
        Assert.Contains("Duplicate PolicyValidationEvent for SingleOrg policy", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationDoesNotExist_ThrowsBadRequest([PolicyUpdate(PolicyType.ActivateAutofill)] PolicyUpdate policyUpdate)
    {
        // Arrange
        var sutProvider = SutProviderFactory();
        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policyUpdate.OrganizationId)
            .Returns(Task.FromResult<OrganizationAbility?>(null));

        // Act
        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(savePolicyModel));

        // Assert
        Assert.Contains("Organization not found", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationCannotUsePolicies_ThrowsBadRequest([PolicyUpdate(PolicyType.ActivateAutofill)] PolicyUpdate policyUpdate)
    {
        var sutProvider = SutProviderFactory();
        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policyUpdate.OrganizationId)
            .Returns(new OrganizationAbility
            {
                Id = policyUpdate.OrganizationId,
                UsePolicies = false
            });

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(savePolicyModel));

        Assert.Contains("cannot use policies", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyIsNull_Throws(
        [PolicyUpdate(PolicyType.RequireSso)] PolicyUpdate policyUpdate)
    {
        // Arrange
        var sutProvider = SutProviderFactory(
            [
                new FakeRequireSsoDependencyEvent(),
                new FakeSingleOrgDependencyEvent()
            ]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        var requireSsoPolicy = new Policy
        {
            Type = PolicyType.RequireSso,
            OrganizationId = policyUpdate.OrganizationId,
            Enabled = false
        };

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([requireSsoPolicy]);

        // Act
        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(savePolicyModel));

        // Assert
        Assert.Contains("Turn on the Single organization policy because it is required for the Require single sign-on authentication policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyNotEnabled_Throws(
        [PolicyUpdate(PolicyType.RequireSso)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy singleOrgPolicy)
    {
        // Arrange
        var sutProvider = SutProviderFactory(
            [
                new FakeRequireSsoDependencyEvent(),
                new FakeSingleOrgDependencyEvent()
            ]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        var requireSsoPolicy = new Policy
        {
            Type = PolicyType.RequireSso,
            OrganizationId = policyUpdate.OrganizationId,
            Enabled = false
        };

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([singleOrgPolicy, requireSsoPolicy]);

        // Act
        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(savePolicyModel));

        // Assert
        Assert.Contains("Turn on the Single organization policy because it is required for the Require single sign-on authentication policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequiredPolicyEnabled_Success(
        [PolicyUpdate(PolicyType.RequireSso)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy)
    {
        // Arrange
        var sutProvider = SutProviderFactory(
            [
                new FakeRequireSsoDependencyEvent(),
                new FakeSingleOrgDependencyEvent()
            ]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        var requireSsoPolicy = new Policy
        {
            Type = PolicyType.RequireSso,
            OrganizationId = policyUpdate.OrganizationId,
            Enabled = false
        };

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([singleOrgPolicy, requireSsoPolicy]);

        // Act
        await sutProvider.Sut.SaveAsync(savePolicyModel);

        // Assert
        await AssertPolicySavedAsync(sutProvider, policyUpdate);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DependentPolicyIsEnabled_Throws(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso)] Policy requireSsoPolicy)
    {
        var sutProvider = SutProviderFactory(
            [
                new FakeRequireSsoDependencyEvent(),
                new FakeSingleOrgDependencyEvent()
            ]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(savePolicyModel));

        Assert.Contains("Turn off the Require single sign-on authentication policy because it requires the Single organization policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_MultipleDependentPoliciesAreEnabled_Throws(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso)] Policy requireSsoPolicy,
        [Policy(PolicyType.MaximumVaultTimeout)] Policy vaultTimeoutPolicy)
    {
        var sutProvider = SutProviderFactory(
            [
                new FakeRequireSsoDependencyEvent(),
                new FakeSingleOrgDependencyEvent(),
                new FakeVaultTimeoutDependencyEvent()
            ]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy, vaultTimeoutPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(savePolicyModel));

        Assert.Contains("Turn off all of the policies that require the Single organization policy", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DependentPolicyNotEnabled_Success(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy currentPolicy,
        [Policy(PolicyType.RequireSso, false)] Policy requireSsoPolicy)
    {
        var sutProvider = SutProviderFactory(
            [
                new FakeRequireSsoDependencyEvent(),
                new FakeSingleOrgDependencyEvent()
            ]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns([currentPolicy, requireSsoPolicy]);

        await sutProvider.Sut.SaveAsync(savePolicyModel);

        await AssertPolicySavedAsync(sutProvider, policyUpdate);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ThrowsOnValidationError([PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate)
    {
        var fakePolicyValidationEvent = new FakeSingleOrgValidationEvent();
        fakePolicyValidationEvent.ValidateAsyncMock(Arg.Any<SavePolicyModel>(), Arg.Any<Policy>()).Returns("Validation error!");
        var sutProvider = SutProviderFactory(
            [new FakeSingleOrgDependencyEvent()],
            [fakePolicyValidationEvent]);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        var singleOrgPolicy = new Policy
        {
            Type = PolicyType.SingleOrg,
            OrganizationId = policyUpdate.OrganizationId,
            Enabled = false
        };

        ArrangeOrganization(sutProvider, policyUpdate);
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policyUpdate.OrganizationId).Returns([singleOrgPolicy]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(savePolicyModel));

        Assert.Contains("Validation error!", badRequestException.Message, StringComparison.OrdinalIgnoreCase);
        await AssertPolicyNotSavedAsync(sutProvider);
    }

    /// <summary>
    /// Returns a new SutProvider with the PolicyDependencyEvents registered in the Sut.
    /// </summary>
    private static SutProvider<VNextSavePolicyCommand> SutProviderFactory(
        IEnumerable<IEnforceDependentPoliciesEvent>? policyDependencyEvents = null,
        IEnumerable<IPolicyValidationEvent>? policyValidationEvents = null)
    {
        var policyEventHandlerFactory = Substitute.For<IPolicyEventHandlerFactory>();

        // Setup factory to return handlers based on type
        policyEventHandlerFactory.GetHandler<IEnforceDependentPoliciesEvent>(Arg.Any<PolicyType>())
            .Returns(callInfo =>
            {
                var policyType = callInfo.Arg<PolicyType>();
                var handler = policyDependencyEvents?.FirstOrDefault(e => e.Type == policyType);
                return handler != null ? OneOf.OneOf<IEnforceDependentPoliciesEvent, None>.FromT0(handler) : OneOf.OneOf<IEnforceDependentPoliciesEvent, None>.FromT1(new None());
            });

        policyEventHandlerFactory.GetHandler<IPolicyValidationEvent>(Arg.Any<PolicyType>())
            .Returns(callInfo =>
            {
                var policyType = callInfo.Arg<PolicyType>();
                var handler = policyValidationEvents?.FirstOrDefault(e => e.Type == policyType);
                return handler != null ? OneOf.OneOf<IPolicyValidationEvent, None>.FromT0(handler) : OneOf.OneOf<IPolicyValidationEvent, None>.FromT1(new None());
            });

        policyEventHandlerFactory.GetHandler<IOnPolicyPreUpdateEvent>(Arg.Any<PolicyType>())
            .Returns(new None());

        policyEventHandlerFactory.GetHandler<IOnPolicyPostUpdateEvent>(Arg.Any<PolicyType>())
            .Returns(new None());

        return new SutProvider<VNextSavePolicyCommand>()
            .WithFakeTimeProvider()
            .SetDependency(policyDependencyEvents ?? [])
            .SetDependency(policyEventHandlerFactory)
            .Create();
    }

    private static void ArrangeOrganization(SutProvider<VNextSavePolicyCommand> sutProvider, PolicyUpdate policyUpdate)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policyUpdate.OrganizationId)
            .Returns(new OrganizationAbility
            {
                Id = policyUpdate.OrganizationId,
                UsePolicies = true
            });
    }

    private static async Task AssertPolicyNotSavedAsync(SutProvider<VNextSavePolicyCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default);
    }

    private static async Task AssertPolicySavedAsync(SutProvider<VNextSavePolicyCommand> sutProvider, PolicyUpdate policyUpdate)
    {
        await sutProvider.GetDependency<IPolicyRepository>().Received(1).UpsertAsync(ExpectedPolicy());

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogPolicyEventAsync(ExpectedPolicy(), EventType.Policy_Updated);

        return;

        Policy ExpectedPolicy() => Arg.Is<Policy>(
            p =>
                p.Type == policyUpdate.Type
                && p.OrganizationId == policyUpdate.OrganizationId
                && p.Enabled == policyUpdate.Enabled
                && p.Data == policyUpdate.Data);
    }
}
