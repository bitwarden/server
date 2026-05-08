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
    public async Task GetOrganizationAbilityAsync_WhenFlagOff_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid orgId,
        OrganizationAbility expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(false);
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
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .DidNotReceiveWithAnyArgs()
            .GetOrganizationAbilityAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_WhenFlagOn_ReturnsFromExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid orgId,
        OrganizationAbility expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(orgId);

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .Received(1)
            .GetOrganizationAbilityAsync(orgId);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceiveWithAnyArgs()
            .GetOrganizationAbilityAsync(default);
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

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_WhenFlagOff_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        OrganizationAbility matchedAbility,
        OrganizationAbility unmatchedAbility)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(false);
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
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .GetOrganizationAbilitiesAsync();
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .DidNotReceiveWithAnyArgs()
            .GetOrganizationAbilitiesAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_WhenFlagOn_ReturnsFromExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        OrganizationAbility ability)
    {
        // Arrange
        var orgIds = new[] { ability.Id };
        var expectedResult = new Dictionary<Guid, OrganizationAbility> { [ability.Id] = ability };
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilitiesAsync(orgIds)
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync(orgIds);

        // Assert
        Assert.Single(result);
        Assert.Equal(ability, result[ability.Id]);
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .Received(1)
            .GetOrganizationAbilitiesAsync(orgIds);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceiveWithAnyArgs()
            .GetOrganizationAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_WhenFlagOff_WhenDuplicateIdsProvided_DoesNotThrowAndReturnsSingleEntry(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        OrganizationAbility ability)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(false);
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
    public async Task GetOrganizationAbilitiesAsync_WhenFlagOff_WhenNoIdsMatched_ReturnsEmptyDictionary(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid missingOrgId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(false);
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>());

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync([missingOrgId]);

        // Assert
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_WhenFlagOff_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(false);

        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .DidNotReceiveWithAnyArgs()
            .UpsertOrganizationAbilityAsync(default);
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_WhenFlagOn_CallsExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceiveWithAnyArgs()
            .UpsertOrganizationAbilityAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationAbilityAsync_WhenFlagOff_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid organizationId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(false);

        // Act
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(organizationId);
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .DidNotReceiveWithAnyArgs()
            .DeleteOrganizationAbilityAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationAbilityAsync_WhenFlagOn_CallsExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid organizationId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(organizationId);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceiveWithAnyArgs()
            .DeleteOrganizationAbilityAsync(default);
    }

    [Theory, BitAutoData]
    public async Task BaseUpsertOrganizationAbilityAsync_WhenFlagOff_CallsServiceBusCache(
        Organization organization)
    {
        // Arrange
        var currentCacheService = CreateCurrentCacheMockService();
        var extendedCacheService = Substitute.For<IOrganizationAbilityCacheService>();
        var providerAbilityCacheService = Substitute.For<IProviderAbilityCacheService>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache).Returns(false);
        var sut = new FeatureRoutedCacheService(currentCacheService, extendedCacheService, providerAbilityCacheService, featureService);

        // Act
        await sut.BaseUpsertOrganizationAbilityAsync(organization);

        // Assert
        await currentCacheService
            .Received(1)
            .BaseUpsertOrganizationAbilityAsync(organization);
        await extendedCacheService
            .DidNotReceiveWithAnyArgs()
            .UpsertOrganizationAbilityAsync(default);
    }

    [Theory, BitAutoData]
    public async Task BaseUpsertOrganizationAbilityAsync_WhenFlagOn_CallsExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.BaseUpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);
    }

    [Theory, BitAutoData]
    public async Task BaseUpsertOrganizationAbilityAsync_WhenFlagOff_AndServiceIsNotServiceBusCache_ThrowsException(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(false);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.BaseUpsertOrganizationAbilityAsync(organization));

        // Assert
        Assert.Equal(ExpectedErrorMessage, ex.Message);
    }

    [Theory, BitAutoData]
    public async Task BaseDeleteOrganizationAbilityAsync_WhenFlagOff_CallsServiceBusCache(
        Guid organizationId)
    {
        // Arrange
        var currentCacheService = CreateCurrentCacheMockService();
        var extendedCacheService = Substitute.For<IOrganizationAbilityCacheService>();
        var providerAbilityCacheService = Substitute.For<IProviderAbilityCacheService>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache).Returns(false);
        var sut = new FeatureRoutedCacheService(currentCacheService, extendedCacheService, providerAbilityCacheService, featureService);

        // Act
        await sut.BaseDeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await currentCacheService
            .Received(1)
            .BaseDeleteOrganizationAbilityAsync(organizationId);
        await extendedCacheService
            .DidNotReceiveWithAnyArgs()
            .DeleteOrganizationAbilityAsync(default);
    }

    [Theory, BitAutoData]
    public async Task BaseDeleteOrganizationAbilityAsync_WhenFlagOn_CallsExtendedCacheService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid organizationId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.BaseDeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task BaseDeleteOrganizationAbilityAsync_WhenFlagOff_AndServiceIsNotServiceBusCache_ThrowsException(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid organizationId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            .Returns(false);

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
