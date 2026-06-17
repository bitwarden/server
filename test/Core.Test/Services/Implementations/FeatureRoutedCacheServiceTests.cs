using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Services;
using Bit.Core.Services.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services.Implementations;

[SutProviderCustomize]
public class FeatureRoutedCacheServiceTests
{

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        IDictionary<Guid, ProviderAbility> expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .GetProviderAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilityAsync_WhenFeatureFlagEnabled_UsesExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid providerId,
        ProviderAbility expectedAbility)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache)
            .Returns(true);
        sutProvider.GetDependency<IProviderAbilityCacheService>()
            .GetProviderAbilityAsync(providerId)
            .Returns(expectedAbility);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilityAsync(providerId);

        // Assert
        Assert.Equal(expectedAbility, result);
        await sutProvider.GetDependency<IProviderAbilityCacheService>()
            .Received(1)
            .GetProviderAbilityAsync(providerId);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetProviderAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilityAsync_WhenFeatureFlagDisabled_UsesInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        ProviderAbility providerAbility)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache)
            .Returns(false);
        var allAbilities = new Dictionary<Guid, ProviderAbility> { [providerAbility.Id] = providerAbility };
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(allAbilities);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilityAsync(providerAbility.Id);

        // Assert
        Assert.Equal(providerAbility, result);
        await sutProvider.GetDependency<IProviderAbilityCacheService>()
            .DidNotReceive()
            .GetProviderAbilityAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .GetProviderAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task UpsertProviderAbilityAsync_WhenFeatureFlagEnabled_UsesExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Provider provider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.UpsertProviderAbilityAsync(provider);

        // Assert
        await sutProvider.GetDependency<IProviderAbilityCacheService>()
            .Received(1)
            .UpsertProviderAbilityAsync(provider);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .UpsertProviderAbilityAsync(Arg.Any<Provider>());
    }

    [Theory, BitAutoData]
    public async Task UpsertProviderAbilityAsync_WhenFeatureFlagDisabled_UsesInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Provider provider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache)
            .Returns(false);

        // Act
        await sutProvider.Sut.UpsertProviderAbilityAsync(provider);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertProviderAbilityAsync(provider);
        await sutProvider.GetDependency<IProviderAbilityCacheService>()
            .DidNotReceive()
            .UpsertProviderAbilityAsync(Arg.Any<Provider>());
    }

    [Theory, BitAutoData]
    public async Task DeleteProviderAbilityAsync_WhenFeatureFlagEnabled_UsesExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid providerId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.DeleteProviderAbilityAsync(providerId);

        // Assert
        await sutProvider.GetDependency<IProviderAbilityCacheService>()
            .Received(1)
            .DeleteProviderAbilityAsync(providerId);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .DeleteProviderAbilityAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task DeleteProviderAbilityAsync_WhenFeatureFlagDisabled_UsesInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid providerId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache)
            .Returns(false);

        // Act
        await sutProvider.Sut.DeleteProviderAbilityAsync(providerId);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteProviderAbilityAsync(providerId);
        await sutProvider.GetDependency<IProviderAbilityCacheService>()
            .DidNotReceive()
            .DeleteProviderAbilityAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_ReturnsOnlyMatchingAbilities(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        ProviderAbility matchedAbility,
        ProviderAbility unmatchedAbility)
    {
        // Arrange
        var allAbilities = new Dictionary<Guid, ProviderAbility>
        {
            [matchedAbility.Id] = matchedAbility,
            [unmatchedAbility.Id] = unmatchedAbility
        };
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(allAbilities);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync([matchedAbility.Id]);

        // Assert
        Assert.Single(result);
        Assert.Equal(matchedAbility, result[matchedAbility.Id]);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenNoIdsMatched_ReturnsEmptyDictionary(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid missingProviderId)
    {
        // Arrange
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(new Dictionary<Guid, ProviderAbility>());

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync([missingProviderId]);

        // Assert
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenDuplicateIdsProvided_DoesNotThrowAndReturnsSingleEntry(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        ProviderAbility ability)
    {
        // Arrange
        var allAbilities = new Dictionary<Guid, ProviderAbility> { [ability.Id] = ability };
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(allAbilities);

        // Act - passing the same ID twice simulates a provider with duplicate entries
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync([ability.Id, ability.Id]);

        // Assert
        Assert.Single(result);
        Assert.Equal(ability, result[ability.Id]);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenFlagOn_ReturnsFromExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        ProviderAbility ability)
    {
        // Arrange
        var providerIds = new[] { ability.Id };
        var expectedResult = new Dictionary<Guid, ProviderAbility> { [ability.Id] = ability };
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache)
            .Returns(true);
        sutProvider.GetDependency<IProviderAbilityCacheService>()
            .GetProviderAbilitiesAsync(providerIds)
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync(providerIds);

        // Assert
        Assert.Single(result);
        Assert.Equal(ability, result[ability.Id]);
        await sutProvider.GetDependency<IProviderAbilityCacheService>()
            .Received(1)
            .GetProviderAbilitiesAsync(providerIds);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceiveWithAnyArgs()
            .GetProviderAbilitiesAsync();
    }
}
