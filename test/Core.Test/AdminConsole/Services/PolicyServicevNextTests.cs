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
    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationDoesNotExist_ThrowsBadRequest(
        Policy policy)
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

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationCannotUsePolicies_ThrowsBadRequest(
        Policy policy)
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

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ThrowsOnValidationError([AdminConsoleFixtures.Policy(PolicyType.SingleOrg)] Policy policy)
    {
        var fakePolicyDefinition = new FakeSingleOrgPolicyDefinition();
        fakePolicyDefinition.ValidateAsyncMock(null, policy).Returns("Validation error!");
        var sutProvider = SutProviderFactory([fakePolicyDefinition]);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policy.OrganizationId)
            .Returns(new OrganizationAbility
            {
                Id = policy.OrganizationId,
                UsePolicies = true
            });

        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(policy.OrganizationId).Returns([]);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Substitute.For<IUserService>(),
                Substitute.For<IOrganizationService>(),
                Guid.NewGuid()));

        Assert.Contains("Validation error!", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
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
}
