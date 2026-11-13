using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.AdminConsole.AbilitiesCache;

[SutProviderCustomize]
public class ExtendedOrganizationAbilityCacheServiceTests
{
    private static string CacheKey(Guid organizationId) =>
        $"{ExtendedOrganizationAbilityCacheConstants.KeyVersion}:{organizationId}";

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_UsesVersionedCacheKey(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        OrganizationAbility ability)
    {
        // Arrange
        SetupCacheReturns(sutProvider, ability);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(ability.Id);

        // Assert
        Assert.Equal(ability, result);
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .GetOrSetAsync<OrganizationAbility?>(
                CacheKey(ability.Id),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationAbility?>, CancellationToken, Task<OrganizationAbility?>>>(),
                token: Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_DoesNotReadUnversionedCacheKey(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        OrganizationAbility ability)
    {
        // Arrange
        SetupCacheReturns(sutProvider, ability);

        // Act
        await sutProvider.Sut.GetOrganizationAbilityAsync(ability.Id);

        // Assert
        await sutProvider.GetDependency<IFusionCache>()
            .DidNotReceive()
            .GetOrSetAsync<OrganizationAbility?>(
                ability.Id.ToString(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationAbility?>, CancellationToken, Task<OrganizationAbility?>>>(),
                token: Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_ReturnsAvailableAbilitiesForRequestedIds(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        OrganizationAbility requestedAbility1,
        OrganizationAbility requestedAbility2,
        OrganizationAbility nonRequestedAbility,
        Guid missingOrganizationId)
    {
        // Arrange
        SetupCacheReturns(sutProvider, requestedAbility1, requestedAbility2, nonRequestedAbility);
        sutProvider.GetDependency<IFusionCache>()
            .GetOrSetAsync<OrganizationAbility?>(
                CacheKey(missingOrganizationId),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationAbility?>, CancellationToken, Task<OrganizationAbility?>>>(),
                token: Arg.Any<CancellationToken>())
            .Returns((OrganizationAbility?)null);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync(
            [requestedAbility1.Id, requestedAbility2.Id, requestedAbility2.Id, missingOrganizationId]);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(requestedAbility1, result[requestedAbility1.Id]);
        Assert.Equal(requestedAbility2, result[requestedAbility2.Id]);
        Assert.DoesNotContain(nonRequestedAbility.Id, result.Keys);
        Assert.DoesNotContain(missingOrganizationId, result.Keys);
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_WritesVersionedCacheKey(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        Organization organization)
    {
        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .SetAsync<OrganizationAbility?>(
                CacheKey(organization.Id),
                Arg.Is<OrganizationAbility?>(a =>
                    a != null &&
                    a.Id == organization.Id &&
                    a.Enabled == organization.Enabled &&
                    a.UseSecretsManager == organization.UseSecretsManager),
                token: Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationAbilityAsync_RemovesVersionedCacheKey(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        Guid organizationId)
    {
        // Act
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .RemoveAsync(CacheKey(organizationId), token: Arg.Any<CancellationToken>());
    }

    private static void SetupCacheReturns(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        params OrganizationAbility[] abilities)
    {
        foreach (var ability in abilities)
        {
            sutProvider.GetDependency<IFusionCache>()
                .GetOrSetAsync<OrganizationAbility?>(
                    CacheKey(ability.Id),
                    Arg.Any<Func<FusionCacheFactoryExecutionContext<OrganizationAbility?>, CancellationToken, Task<OrganizationAbility?>>>(),
                    token: Arg.Any<CancellationToken>())
                .Returns(ability);
        }
    }
}
