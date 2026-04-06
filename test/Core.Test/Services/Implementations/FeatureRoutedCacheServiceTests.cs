using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Services.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Services.Implementations;

[SutProviderCustomize]
public class FeatureRoutedCacheServiceTests
{
    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        IDictionary<Guid, OrganizationAbility> expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .GetOrganizationAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid orgId,
        OrganizationAbility expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(orgId);

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .GetOrganizationAbilityAsync(orgId);
    }

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
    public async Task GetProviderAbilityAsync_WhenProviderExists_ReturnsAbility(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        ProviderAbility providerAbility)
    {
        // Arrange
        var allAbilities = new Dictionary<Guid, ProviderAbility> { [providerAbility.Id] = providerAbility };
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(allAbilities);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilityAsync(providerAbility.Id);

        // Assert
        Assert.Equal(providerAbility, result);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilityAsync_WhenProviderDoesNotExist_ReturnsNull(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid providerId)
    {
        // Arrange
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(new Dictionary<Guid, ProviderAbility>());

        // Act
        var result = await sutProvider.Sut.GetProviderAbilityAsync(providerId);

        // Assert
        Assert.Null(result);
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
    public async Task GetOrganizationAbilitiesAsync_ReturnsOnlyMatchingAbilities(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        OrganizationAbility matchedAbility,
        OrganizationAbility unmatchedAbility)
    {
        // Arrange
        var allAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            [matchedAbility.Id] = matchedAbility,
            [unmatchedAbility.Id] = unmatchedAbility
        };
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(allAbilities);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync([matchedAbility.Id]);

        // Assert
        Assert.Single(result);
        Assert.Equal(matchedAbility, result[matchedAbility.Id]);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_WhenDuplicateIdsProvided_DoesNotThrowAndReturnsSingleEntry(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        OrganizationAbility ability)
    {
        // Arrange
        var allAbilities = new Dictionary<Guid, OrganizationAbility> { [ability.Id] = ability };
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(allAbilities);

        // Act - passing the same ID twice simulates a user with duplicate org memberships
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync([ability.Id, ability.Id]);

        // Assert
        Assert.Single(result);
        Assert.Equal(ability, result[ability.Id]);
    }


    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_WhenNoIdsMatched_ReturnsEmptyDictionary(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid missingOrgId)
    {
        // Arrange
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>());

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync([missingOrgId]);

        // Assert
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);
    }

    [Theory, BitAutoData]
    public async Task UpsertProviderAbilityAsync_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Provider provider)
    {
        // Act
        await sutProvider.Sut.UpsertProviderAbilityAsync(provider);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertProviderAbilityAsync(provider);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationAbilityAsync_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid organizationId)
    {
        // Act
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task DeleteProviderAbilityAsync_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid providerId)
    {
        // Act
        await sutProvider.Sut.DeleteProviderAbilityAsync(providerId);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteProviderAbilityAsync(providerId);
    }

    [Theory, BitAutoData]
    public async Task BaseUpsertOrganizationAbilityAsync_CallsServiceBusCache(
        Organization organization)
    {
        // Arrange
        var currentCacheService = CreateCurrentCacheMockService();
        var sut = new FeatureRoutedCacheService(currentCacheService);

        // Act
        await sut.BaseUpsertOrganizationAbilityAsync(organization);

        // Assert
        await currentCacheService
            .Received(1)
            .BaseUpsertOrganizationAbilityAsync(organization);
    }

    [Theory, BitAutoData]
    public async Task BaseUpsertOrganizationAbilityAsync_WhenServiceIsNotServiceBusCache_ThrowsException(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.BaseUpsertOrganizationAbilityAsync(organization));

        // Assert
        Assert.Equal(ExpectedErrorMessage, ex.Message);
    }

    [Theory, BitAutoData]
    public async Task BaseDeleteOrganizationAbilityAsync_CallsServiceBusCache(
        Guid organizationId)
    {
        // Arrange
        var currentCacheService = CreateCurrentCacheMockService();
        var sut = new FeatureRoutedCacheService(currentCacheService);

        // Act
        await sut.BaseDeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await currentCacheService
            .Received(1)
            .BaseDeleteOrganizationAbilityAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task BaseDeleteOrganizationAbilityAsync_WhenServiceIsNotServiceBusCache_ThrowsException(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid organizationId)
    {
        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutProvider.Sut.BaseDeleteOrganizationAbilityAsync(organizationId));

        // Assert
        Assert.Equal(ExpectedErrorMessage, ex.Message);
    }

    /// <summary>
    /// Our SUT uses a method that is not part of IVCurrentInMemoryApplicationCacheService,
    /// so AutoFixture's auto-created mock won't work.
    /// </summary>
    private static InMemoryServiceBusApplicationCacheService CreateCurrentCacheMockService()
    {
        return Substitute.For<InMemoryServiceBusApplicationCacheService>(
            Substitute.For<IOrganizationRepository>(),
            Substitute.For<IProviderRepository>(),
            new GlobalSettings
            {
                ProjectName = "BitwardenTest",
                ServiceBus = new GlobalSettings.ServiceBusSettings
                {
                    ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
                    ApplicationCacheTopicName = "test-topic",
                    ApplicationCacheSubscriptionName = "test-subscription"
                }
            });
    }

    private static string ExpectedErrorMessage =>
        "Expected inMemoryApplicationCacheService to be of type InMemoryServiceBusApplicationCacheService";
}
