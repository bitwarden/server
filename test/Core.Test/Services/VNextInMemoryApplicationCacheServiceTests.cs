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
        var firstResult = await sutProvider.Sut.GetOrganizationAbilitiesAsync();
        var secondResult = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.Same(firstResult, secondResult);
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
        var firstResult = await sutProvider.Sut.GetProviderAbilitiesAsync();
        var secondResult = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.Same(firstResult, secondResult);
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
    public async Task DeleteOrganizationAbilityAsync_NullSource_DoesNotThrow(
        Guid organizationId,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Act & Assert
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(organizationId);
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
    public async Task DeleteProviderAbilityAsync_NullSource_DoesNotThrow(
        Guid providerId,
        SutProvider<VNextInMemoryApplicationCacheService> sutProvider)
    {
        // Act & Assert
        await sutProvider.Sut.DeleteProviderAbilityAsync(providerId);
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
        var firstResult = results.First();
        Assert.Equal(iterationCount, results.Count);
        Assert.All(results, result => Assert.Same(firstResult, result));
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetManyAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_AfterRefreshInterval_RefreshesFromRepository(
        List<OrganizationAbility> organizationAbilities,
        List<OrganizationAbility> updatedAbilities)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var orgRepo = Substitute.For<IOrganizationRepository>();
        var providerRepo = Substitute.For<IProviderRepository>();

        orgRepo.GetManyAbilitiesAsync().Returns(organizationAbilities, updatedAbilities);

        var sut = new VNextInMemoryApplicationCacheService(orgRepo, providerRepo, fakeTimeProvider);

        var firstResult = await sut.GetOrganizationAbilitiesAsync();

        const int pastIntervalInMinutes = 11;
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(pastIntervalInMinutes));

        // Act
        var secondResult = await sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.NotSame(firstResult, secondResult);
        Assert.Equal(updatedAbilities.Count, secondResult.Count);
        await orgRepo.Received(2).GetManyAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_AfterRefreshInterval_RefreshesFromRepository(
        List<ProviderAbility> providerAbilities,
        List<ProviderAbility> updatedAbilities)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var orgRepo = Substitute.For<IOrganizationRepository>();
        var providerRepo = Substitute.For<IProviderRepository>();

        providerRepo.GetManyAbilitiesAsync().Returns(providerAbilities, updatedAbilities);

        var sut = new VNextInMemoryApplicationCacheService(orgRepo, providerRepo, fakeTimeProvider);

        var firstResult = await sut.GetProviderAbilitiesAsync();

        const int pastIntervalMinutes = 11;
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(pastIntervalMinutes));

        // Act
        var secondResult = await sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.NotSame(firstResult, secondResult);
        Assert.Equal(updatedAbilities.Count, secondResult.Count);
        await providerRepo.Received(2).GetManyAbilitiesAsync();
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
        var fakeTimeProvider = new FakeTimeProvider();
        var orgRepo = Substitute.For<IOrganizationRepository>();
        var providerRepo = Substitute.For<IProviderRepository>();

        orgRepo.GetManyAbilitiesAsync().Returns(organizationAbilities);

        var sut = new VNextInMemoryApplicationCacheService(orgRepo, providerRepo, fakeTimeProvider);

        var firstResult = await sut.GetOrganizationAbilitiesAsync();

        fakeTimeProvider.Advance(TimeSpan.FromMinutes(pastIntervalInMinutes));

        // Act
        var secondResult = await sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.Same(firstResult, secondResult);
        Assert.Equal(organizationAbilities.Count, secondResult.Count);
        await orgRepo.Received(expectCacheHit).GetManyAbilitiesAsync();
    }

    [Theory]
    [BitMemberAutoData(nameof(WhenCacheIsWithinIntervalTestCases))]
    public async Task GetProviderAbilitiesAsync_WhenCacheIsWithinInterval(
        int pastIntervalInMinutes,
        int expectCacheHit,
        List<ProviderAbility> providerAbilities)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var orgRepo = Substitute.For<IOrganizationRepository>();
        var providerRepo = Substitute.For<IProviderRepository>();

        providerRepo.GetManyAbilitiesAsync().Returns(providerAbilities);

        var sut = new VNextInMemoryApplicationCacheService(orgRepo, providerRepo, fakeTimeProvider);

        var firstResult = await sut.GetProviderAbilitiesAsync();

        fakeTimeProvider.Advance(TimeSpan.FromMinutes(pastIntervalInMinutes));

        // Act
        var secondResult = await sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.Same(firstResult, secondResult);
        Assert.Equal(providerAbilities.Count, secondResult.Count);
        await providerRepo.Received(expectCacheHit).GetManyAbilitiesAsync();
    }

}
