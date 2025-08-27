using System.Collections.Concurrent;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class VNextInMemoryApplicationCacheServiceTests

{
    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_FirstCall_LoadsFromRepository(
        ICollection<OrganizationAbility> organizationAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(organizationAbilities);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.IsType<ConcurrentDictionary<Guid, OrganizationAbility>>(result);
        Assert.Equal(organizationAbilities.Count, result.Count);
        foreach (var ability in organizationAbilities)
        {
            Assert.True(result.TryGetValue(ability.Id, out var actualAbility));
            Assert.Equal(ability, actualAbility);
        }
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetManyAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_SecondCall_UsesCachedValue(
        List<OrganizationAbility> organizationAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(organizationAbilities);

        // Act
        var firstCall = await sutProvider.Sut.GetOrganizationAbilitiesAsync();
        var secondCall = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.Same(firstCall, secondCall);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetManyAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_ExistingId_ReturnsAbility(
        List<OrganizationAbility> organizationAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        var targetAbility = organizationAbilities.First();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(organizationAbilities);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(targetAbility.Id);

        // Assert
        Assert.Equal(targetAbility, result);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_NonExistingId_ReturnsNull(
        List<OrganizationAbility> organizationAbilities,
        Guid nonExistingId,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(organizationAbilities);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(nonExistingId);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_FirstCall_LoadsFromRepository(
        List<ProviderAbility> providerAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IProviderRepository>()
            .GetManyAbilitiesAsync()
            .Returns(providerAbilities);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.IsType<ConcurrentDictionary<Guid, ProviderAbility>>(result);
        Assert.Equal(providerAbilities.Count, result.Count);
        foreach (var ability in providerAbilities)
        {
            Assert.True(result.TryGetValue(ability.Id, out var actualAbility));
            Assert.Equal(ability, actualAbility);
        }
        await sutProvider.GetDependency<IProviderRepository>().Received(1).GetManyAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_SecondCall_UsesCachedValue(
        List<ProviderAbility> providerAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IProviderRepository>()
            .GetManyAbilitiesAsync()
            .Returns(providerAbilities);

        // Act
        var firstCall = await sutProvider.Sut.GetProviderAbilitiesAsync();
        var secondCall = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.Same(firstCall, secondCall);
        await sutProvider.GetDependency<IProviderRepository>().Received(1).GetManyAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_NewOrganization_AddsToCache(
        Organization organization,
        List<OrganizationAbility> existingAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(existingAbilities);
        await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync();
        Assert.True(result.ContainsKey(organization.Id));
        Assert.Equal(organization.Id, result[organization.Id].Id);
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_ExistingOrganization_UpdatesCache(
        Organization organization,
        List<OrganizationAbility> existingAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        existingAbilities.Add(new OrganizationAbility { Id = organization.Id });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(existingAbilities);
        await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync();
        Assert.True(result.ContainsKey(organization.Id));
        Assert.Equal(organization.Id, result[organization.Id].Id);
    }

    [Theory, BitAutoData]
    public async Task UpsertProviderAbilityAsync_NewProvider_AddsToCache(
        Provider provider,
        List<ProviderAbility> existingAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IProviderRepository>()
            .GetManyAbilitiesAsync()
            .Returns(existingAbilities);
        await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Act
        await sutProvider.Sut.UpsertProviderAbilityAsync(provider);

        // Assert
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync();
        Assert.True(result.ContainsKey(provider.Id));
        Assert.Equal(provider.Id, result[provider.Id].Id);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationAbilityAsync_ExistingId_RemovesFromCache(
        List<OrganizationAbility> organizationAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        var targetAbility = organizationAbilities.First();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(organizationAbilities);
        await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Act
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(targetAbility.Id);

        // Assert
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync();
        Assert.False(result.ContainsKey(targetAbility.Id));
    }


    [Theory, BitAutoData]
    public async Task DeleteProviderAbilityAsync_ExistingId_RemovesFromCache(
        List<ProviderAbility> providerAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        var targetAbility = providerAbilities.First();
        sutProvider.GetDependency<IProviderRepository>()
            .GetManyAbilitiesAsync()
            .Returns(providerAbilities);
        await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Act
        await sutProvider.Sut.DeleteProviderAbilityAsync(targetAbility.Id);

        // Assert
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync();
        Assert.False(result.ContainsKey(targetAbility.Id));
    }

    [Theory, BitAutoData]
    public async Task ConcurrentAccess_GetOrganizationAbilities_ThreadSafe(
        List<OrganizationAbility> organizationAbilities,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(organizationAbilities);

        var results = new ConcurrentBag<IDictionary<Guid, OrganizationAbility>>();

        const int iterationCount = 100;


        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, iterationCount),
            async (_, _) =>
            {
                var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync();
                results.Add(result);
            });

        // Assert
        var firstCall = results.First();
        Assert.Equal(iterationCount, results.Count);
        Assert.All(results, result => Assert.Same(firstCall, result));
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetManyAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_AfterRefreshInterval_RefreshesFromRepository(
        List<OrganizationAbility> organizationAbilities,
        List<OrganizationAbility> updatedAbilities)
    {
        // Arrange
        var sutProvider = new SutProvider<VNextInMemoryApplicationCacheService>()
            .WithFakeTimeProvider()
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(organizationAbilities, updatedAbilities);

        var firstCall = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        const int pastIntervalInMinutes = 11;
        SimulateTimeLapseAfterFirstCall(sutProvider, pastIntervalInMinutes);

        // Act
        var secondCall = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.NotSame(firstCall, secondCall);
        Assert.Equal(updatedAbilities.Count, secondCall.Count);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(2).GetManyAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_AfterRefreshInterval_RefreshesFromRepository(
        List<ProviderAbility> providerAbilities,
        List<ProviderAbility> updatedAbilities)
    {
        // Arrange
        var sutProvider = new SutProvider<VNextInMemoryApplicationCacheService>()
            .WithFakeTimeProvider()
            .Create();

        sutProvider.GetDependency<IProviderRepository>()
            .GetManyAbilitiesAsync()
            .Returns(providerAbilities, updatedAbilities);

        var firstCall = await sutProvider.Sut.GetProviderAbilitiesAsync();
        const int pastIntervalMinutes = 15;
        SimulateTimeLapseAfterFirstCall(sutProvider, pastIntervalMinutes);

        // Act
        var secondCall = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.NotSame(firstCall, secondCall);
        Assert.Equal(updatedAbilities.Count, secondCall.Count);
        await sutProvider.GetDependency<IProviderRepository>().Received(2).GetManyAbilitiesAsync();
    }

    public static IEnumerable<object[]> WhenCacheIsWithinIntervalTestCases =>
    [
        [5, 1],
        [10, 1],
    ];

    [Theory]
    [BitMemberAutoData(nameof(WhenCacheIsWithinIntervalTestCases))]
    public async Task GetOrganizationAbilitiesAsync_WhenCacheIsWithinInterval(
        int pastIntervalInMinutes,
        int expectCacheHit,
        List<OrganizationAbility> organizationAbilities)
    {
        // Arrange
        var sutProvider = new SutProvider<VNextInMemoryApplicationCacheService>()
            .WithFakeTimeProvider()
            .Create();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyAbilitiesAsync()
            .Returns(organizationAbilities);

        var firstCall = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        SimulateTimeLapseAfterFirstCall(sutProvider, pastIntervalInMinutes);

        // Act
        var secondCall = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.Same(firstCall, secondCall);
        Assert.Equal(organizationAbilities.Count, secondCall.Count);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(expectCacheHit).GetManyAbilitiesAsync();
    }

    [Theory]
    [BitMemberAutoData(nameof(WhenCacheIsWithinIntervalTestCases))]
    public async Task GetProviderAbilitiesAsync_WhenCacheIsWithinInterval(
        int pastIntervalInMinutes,
        int expectCacheHit,
        List<ProviderAbility> providerAbilities)
    {
        // Arrange
        var sutProvider = new SutProvider<VNextInMemoryApplicationCacheService>()
            .WithFakeTimeProvider()
            .Create();

        sutProvider.GetDependency<IProviderRepository>()
            .GetManyAbilitiesAsync()
            .Returns(providerAbilities);

        var firstCall = await sutProvider.Sut.GetProviderAbilitiesAsync();

        SimulateTimeLapseAfterFirstCall(sutProvider, pastIntervalInMinutes);

        // Act
        var secondCall = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.Same(firstCall, secondCall);
        Assert.Equal(providerAbilities.Count, secondCall.Count);
        await sutProvider.GetDependency<IProviderRepository>().Received(expectCacheHit).GetManyAbilitiesAsync();
    }

    private static void SimulateTimeLapseAfterFirstCall(SutProvider<VNextInMemoryApplicationCacheService> sutProvider, int pastIntervalInMinutes) =>
        sutProvider
            .GetDependency<FakeTimeProvider>()
            .Advance(TimeSpan.FromMinutes(pastIntervalInMinutes));

}
