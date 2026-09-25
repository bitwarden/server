using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PreAccess;

public class PreAccessEnforcerQueryTests
{
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService =
        Substitute.For<IOrganizationAbilityCacheService>();
    private readonly IPolicyRepository _policyRepository = Substitute.For<IPolicyRepository>();
    private readonly IProviderUserRepository _providerUserRepository = Substitute.For<IProviderUserRepository>();

    private PreAccessEnforcerQuery CreateSut() => new(
        _organizationAbilityCacheService,
        _policyRepository,
        _providerUserRepository,
        [new SingleOrganizationPolicyRequirementFactory(), new RequireTwoFactorPolicyRequirementFactory()]);

    [Theory, BitAutoData]
    public async Task RunAsync_OrganizationNotFound_Throws(Guid organizationId)
    {
        // Arrange
        _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId).Returns((OrganizationAbility?)null);

        // Act & Assert
        await Assert.ThrowsAsync<PreAccessOrganizationNotFoundException>(() => CreateSut().RunAsync(organizationId));
        await _policyRepository.DidNotReceiveWithAnyArgs().GetManyByOrganizationIdAsync(default);
    }

    [Theory]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    public async Task RunAsync_PoliciesUnavailable_ReturnsNotEnforced_WithoutLoadingData(
        bool enabled, bool usePolicies, Guid organizationId, Guid userId)
    {
        // Arrange
        ArrangeOrganizationAbility(organizationId, enabled, usePolicies);

        // Act
        var enforcer = await CreateSut().RunAsync(organizationId);

        // Assert
        Assert.False(enforcer.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.User).Enforced);
        Assert.False(enforcer.Evaluate(PolicyType.TwoFactorAuthentication, userId, OrganizationUserType.User).Enforced);
        await _policyRepository.DidNotReceiveWithAnyArgs().GetManyByOrganizationIdAsync(default);
        await _providerUserRepository.DidNotReceiveWithAnyArgs().GetManyByOrganizationAsync(default);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_EnabledPolicies_ReturnsEnforcerLoadedWithOrganizationState(
        Guid organizationId, Guid userId, Guid providerUserId)
    {
        // Arrange
        ArrangeOrganizationAbility(organizationId, enabled: true, usePolicies: true);
        _policyRepository.GetManyByOrganizationIdAsync(organizationId).Returns(
        [
            CreatePolicy(organizationId, PolicyType.SingleOrg, enabled: true),
            CreatePolicy(organizationId, PolicyType.TwoFactorAuthentication, enabled: false)
        ]);
        _providerUserRepository.GetManyByOrganizationAsync(organizationId).Returns(
        [
            new ProviderUser { UserId = providerUserId },
            new ProviderUser { UserId = null }
        ]);

        // Act
        var enforcer = await CreateSut().RunAsync(organizationId);

        // Assert
        Assert.True(enforcer.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.User).Enforced);
        Assert.False(enforcer.Evaluate(PolicyType.SingleOrg, userId, OrganizationUserType.Owner).Enforced);
        Assert.False(enforcer.Evaluate(PolicyType.SingleOrg, providerUserId, OrganizationUserType.User).Enforced);
        Assert.False(enforcer.Evaluate(PolicyType.TwoFactorAuthentication, userId, OrganizationUserType.User).Enforced);
        await _policyRepository.Received(1).GetManyByOrganizationIdAsync(organizationId);
        await _providerUserRepository.Received(1).GetManyByOrganizationAsync(organizationId);
    }

    private void ArrangeOrganizationAbility(Guid organizationId, bool enabled, bool usePolicies) =>
        _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { Id = organizationId, Enabled = enabled, UsePolicies = usePolicies });

    private static Policy CreatePolicy(Guid organizationId, PolicyType type, bool enabled) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Type = type, Enabled = enabled };
}
