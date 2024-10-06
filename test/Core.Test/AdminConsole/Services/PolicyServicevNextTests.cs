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
using AdminConsoleFixtures = Bit.Core.Test.AdminConsole.AutoFixture;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Services;

public class PolicyServicevNextTests
{
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
    public async Task SaveAsync_ThrowsOnValidationError([AdminConsoleFixtures.Policy(PolicyType.SingleOrg)] Policy policy)
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
