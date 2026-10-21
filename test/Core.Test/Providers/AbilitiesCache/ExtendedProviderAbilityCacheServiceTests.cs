using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.AdminConsole.AbilitiesCache;

[SutProviderCustomize]
public class ExtendedProviderAbilityCacheServiceTests
{
    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_ReturnsAvailableAbilitiesForRequestedIds(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        ProviderAbility requestedAbility1,
        ProviderAbility requestedAbility2,
        ProviderAbility nonRequestedAbility,
        ProviderAbility nonExistAbility)
    {
        // Arrange
        SetupCacheReturns(sutProvider, requestedAbility1, requestedAbility2, nonRequestedAbility);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync([requestedAbility1.Id, requestedAbility2.Id, nonExistAbility.Id]);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(requestedAbility1, result[requestedAbility1.Id]);
        Assert.Equal(requestedAbility2, result[requestedAbility2.Id]);
        Assert.DoesNotContain(nonRequestedAbility.Id, result.Keys);
        Assert.DoesNotContain(nonExistAbility.Id, result.Keys);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenProviderNotFound_ExcludesNullAbilities(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        ProviderAbility ability,
        Guid missingProviderId)
    {
        // Arrange
        SetupCacheReturns(sutProvider, ability);
        sutProvider.GetDependency<IFusionCache>()
            .GetOrSetAsync<ProviderAbility?>(
                $"{missingProviderId}",
                Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                token: Arg.Any<CancellationToken>())
            .Returns((ProviderAbility?)null);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync([ability.Id, missingProviderId]);

        // Assert
        Assert.Single(result);
        Assert.Equal(ability, result[ability.Id]);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenDuplicateIdsProvided_DoesNotThrowAndReturnsSingleEntry(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        ProviderAbility ability)
    {
        // Arrange
        SetupCacheReturns(sutProvider, ability);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync([ability.Id, ability.Id, ability.Id]);

        // Assert
        Assert.Single(result);
        Assert.Equal(ability, result[ability.Id]);
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .GetOrSetAsync<ProviderAbility?>(
                $"{ability.Id}",
                Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                token: Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenEmptyList_ReturnsEmptyDictionary(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync([]);

        // Assert
        Assert.Empty(result);
        await sutProvider.GetDependency<IProviderRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetAbilityAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenAllProvidersNotFound_ReturnsEmptyDictionary(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        Guid missingId1,
        Guid missingId2)
    {
        // Arrange
        sutProvider.GetDependency<IFusionCache>()
            .GetOrSetAsync<ProviderAbility?>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                token: Arg.Any<CancellationToken>())
            .Returns((ProviderAbility?)null);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync([missingId1, missingId2]);

        // Assert
        Assert.Empty(result);
    }

    private static void SetupCacheReturns(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        params ProviderAbility[] abilities)
    {
        foreach (var ability in abilities)
        {
            sutProvider.GetDependency<IFusionCache>()
                .GetOrSetAsync<ProviderAbility?>(
                    $"{ability.Id}",
                    Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                    token: Arg.Any<CancellationToken>())
                .Returns(ability);
        }
    }
}
