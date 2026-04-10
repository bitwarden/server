using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

using ProviderEntity = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Core.Test.AdminConsole.AbilitiesCache;

[SutProviderCustomize]
public class ExtendedProviderAbilityCacheServiceTests
{
    [Theory, BitAutoData]
    public async Task GetProviderAbilityAsync_OnCacheHit_ReturnsWithoutCallingRepository(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        ProviderAbility cachedAbility)
    {
        // Arrange
        sutProvider.GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Any<string>(),
                factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                options: Arg.Any<FusionCacheEntryOptions?>(),
                tags: Arg.Any<IEnumerable<string>?>())
            .Returns(new ValueTask<ProviderAbility?>(cachedAbility));

        // Act
        var result = await sutProvider.Sut.GetProviderAbilityAsync(cachedAbility.Id);

        // Assert
        Assert.Equal(cachedAbility, result);
        await sutProvider.GetDependency<IProviderRepository>()
            .DidNotReceive()
            .GetAbilityAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .GetOrSetAsync<ProviderAbility?>(
                Arg.Is<string>(k => k == cachedAbility.Id.ToString()),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilityAsync_OnCacheMiss_QueriesRepositoryAndCaches(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        Guid providerId,
        ProviderAbility repositoryAbility)
    {
        // Arrange
        sutProvider.GetDependency<IProviderRepository>()
            .GetAbilityAsync(providerId)
            .Returns(repositoryAbility);

        sutProvider.GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Any<string>(),
                factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                options: Arg.Any<FusionCacheEntryOptions?>(),
                tags: Arg.Any<IEnumerable<string>?>())
            .Returns(callInfo =>
            {
                var factory = callInfo.ArgAt<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(1);
                return new ValueTask<ProviderAbility?>(factory.Invoke(null!, CancellationToken.None));
            });

        // Act
        var result = await sutProvider.Sut.GetProviderAbilityAsync(providerId);

        // Assert
        Assert.Equal(repositoryAbility, result);
        await sutProvider.GetDependency<IProviderRepository>()
            .Received(1)
            .GetAbilityAsync(providerId);
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .GetOrSetAsync<ProviderAbility?>(
                Arg.Is<string>(k => k == providerId.ToString()),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilityAsync_WhenProviderDoesNotExist_ReturnsNullAndCachesIt(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        Guid providerId)
    {
        // Arrange
        sutProvider.GetDependency<IProviderRepository>()
            .GetAbilityAsync(providerId)
            .Returns((ProviderAbility?)null);

        sutProvider.GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Any<string>(),
                factory: Arg.Any<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(),
                options: Arg.Any<FusionCacheEntryOptions?>(),
                tags: Arg.Any<IEnumerable<string>?>())
            .Returns(callInfo =>
            {
                var factory = callInfo.ArgAt<Func<FusionCacheFactoryExecutionContext<ProviderAbility?>, CancellationToken, Task<ProviderAbility?>>>(1);
                return new ValueTask<ProviderAbility?>(factory.Invoke(null!, CancellationToken.None));
            });

        // Act
        var result = await sutProvider.Sut.GetProviderAbilityAsync(providerId);

        // Assert - null is cached to prevent database thrashing for non-existent providers
        Assert.Null(result);
        await sutProvider.GetDependency<IProviderRepository>()
            .Received(1)
            .GetAbilityAsync(providerId);
    }

    [Theory, BitAutoData]
    public async Task UpsertProviderAbilityAsync_SetsUpdatedAbilityInCache(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        ProviderEntity provider)
    {
        // Act
        await sutProvider.Sut.UpsertProviderAbilityAsync(provider);

        // Assert - SetAsync updates the cache entry directly with the new data
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .SetAsync(
                Arg.Is<string>(k => k == provider.Id.ToString()),
                Arg.Is<ProviderAbility>(a => a.Id == provider.Id),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task DeleteProviderAbilityAsync_RemovesCacheEntry(
        SutProvider<ExtendedProviderAbilityCacheService> sutProvider,
        Guid providerId)
    {
        // Act
        await sutProvider.Sut.DeleteProviderAbilityAsync(providerId);

        // Assert
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .RemoveAsync(
                Arg.Is<string>(k => k == providerId.ToString()),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>());
    }
}
