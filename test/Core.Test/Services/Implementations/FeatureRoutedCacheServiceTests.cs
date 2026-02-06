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
    public async Task GetOrganizationAbilitiesAsync_WhenFeatureIsEnabled_ReturnsFromVNextService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        IDictionary<Guid, OrganizationAbility> expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);
        sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .GetOrganizationAbilitiesAsync();

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetOrganizationAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilitiesAsync_WhenFeatureIsDisabled_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        IDictionary<Guid, OrganizationAbility> expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);
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

        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetOrganizationAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_WhenFeatureIsEnabled_ReturnsFromVNextService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid orgId,
        OrganizationAbility expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);
        sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(orgId);

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .GetOrganizationAbilityAsync(orgId);

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetOrganizationAbilityAsync(orgId);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_WhenFeatureIsDisabled_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid orgId,
        OrganizationAbility expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
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

        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetOrganizationAbilityAsync(orgId);
    }

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenFeatureIsEnabled_ReturnsFromVNextService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        IDictionary<Guid, ProviderAbility> expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);
        sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .GetProviderAbilitiesAsync();

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetProviderAbilitiesAsync();
    }


    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_WhenFeatureIsDisabled_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        IDictionary<Guid, ProviderAbility> expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);
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

        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetProviderAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_WhenFeatureIsEnabled_CallsVNextService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetProviderAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_WhenFeatureIsDisabled_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);

        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);

        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .DidNotReceive()
            .GetProviderAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task UpsertProviderAbilityAsync_WhenFeatureIsEnabled_CallsVNextService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Provider provider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.UpsertProviderAbilityAsync(provider);

        // Assert
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertProviderAbilityAsync(provider);

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .UpsertProviderAbilityAsync(provider);
    }

    [Theory, BitAutoData]
    public async Task UpsertProviderAbilityAsync_WhenFeatureIsDisabled_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Provider provider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);

        // Act
        await sutProvider.Sut.UpsertProviderAbilityAsync(provider);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertProviderAbilityAsync(provider);

        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .DidNotReceive()
            .UpsertProviderAbilityAsync(provider);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationAbilityAsync_WhenFeatureIsEnabled_CallsVNextService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid organizationId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(organizationId);

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .DeleteOrganizationAbilityAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationAbilityAsync_WhenFeatureIsDisabled_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid organizationId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);

        // Act
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(organizationId);

        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .DidNotReceive()
            .DeleteOrganizationAbilityAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task DeleteProviderAbilityAsync_WhenFeatureIsEnabled_CallsVNextService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid providerId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.DeleteProviderAbilityAsync(providerId);

        // Assert
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteProviderAbilityAsync(providerId);

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .DeleteProviderAbilityAsync(providerId);
    }

    [Theory, BitAutoData]
    public async Task DeleteProviderAbilityAsync_WhenFeatureIsDisabled_CallsInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Guid providerId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);

        // Act
        await sutProvider.Sut.DeleteProviderAbilityAsync(providerId);

        // Assert
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteProviderAbilityAsync(providerId);

        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .DidNotReceive()
            .DeleteProviderAbilityAsync(providerId);
    }

    [Theory, BitAutoData]
    public async Task BaseUpsertOrganizationAbilityAsync_WhenFeatureIsEnabled_CallsVNextService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.BaseUpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .UpsertOrganizationAbilityAsync(organization);
    }

    [Theory, BitAutoData]
    public async Task BaseUpsertOrganizationAbilityAsync_WhenFeatureIsDisabled_CallsServiceBusCache(
        Organization organization)
    {
        // Arrange
        var featureService = Substitute.For<IFeatureService>();

        var currentCacheService = CreateCurrentCacheMockService();

        featureService
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);

        var sutProvider = Substitute.For<FeatureRoutedCacheService>(
            featureService,
            Substitute.For<IVNextInMemoryApplicationCacheService>(),
            currentCacheService,
            Substitute.For<IApplicationCacheServiceBusMessaging>());

        // Act
        await sutProvider.BaseUpsertOrganizationAbilityAsync(organization);

        // Assert
        await currentCacheService
            .Received(1)
            .BaseUpsertOrganizationAbilityAsync(organization);
    }

    /// <summary>
    /// Our SUT is using a method that is not part of the IVCurrentInMemoryApplicationCacheService,
    /// so AutoFixture’s auto-created mock won’t work.
    /// </summary>
    /// <returns></returns>
    private static InMemoryServiceBusApplicationCacheService CreateCurrentCacheMockService()
    {
        var currentCacheService = Substitute.For<InMemoryServiceBusApplicationCacheService>(
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
        return currentCacheService;
    }

    [Theory, BitAutoData]
    public async Task BaseUpsertOrganizationAbilityAsync_WhenFeatureIsDisabled_AndServiceIsNotServiceBusCache_ThrowsException(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.BaseUpsertOrganizationAbilityAsync(organization));

        // Assert
        Assert.Equal(
            ExpectedErrorMessage,
            ex.Message);
    }

    private static string ExpectedErrorMessage
    {
        get => "Expected inMemoryApplicationCacheService to be of type InMemoryServiceBusApplicationCacheService";
    }

    [Theory, BitAutoData]
    public async Task BaseDeleteOrganizationAbilityAsync_WhenFeatureIsEnabled_CallsVNextService(
            SutProvider<FeatureRoutedCacheService> sutProvider,
            Guid organizationId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(true);

        // Act
        await sutProvider.Sut.BaseDeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(organizationId);

        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .DidNotReceive()
            .DeleteOrganizationAbilityAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task BaseDeleteOrganizationAbilityAsync_WhenFeatureIsDisabled_CallsServiceBusCache(
        Guid organizationId)
    {
        // Arrange
        var featureService = Substitute.For<IFeatureService>();

        var currentCacheService = CreateCurrentCacheMockService();

        featureService
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);

        var sutProvider = Substitute.For<FeatureRoutedCacheService>(
            featureService,
            Substitute.For<IVNextInMemoryApplicationCacheService>(),
            currentCacheService,
            Substitute.For<IApplicationCacheServiceBusMessaging>());

        // Act
        await sutProvider.BaseDeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await currentCacheService
            .Received(1)
            .BaseDeleteOrganizationAbilityAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task
        BaseDeleteOrganizationAbilityAsync_WhenFeatureIsDisabled_AndServiceIsNotServiceBusCache_ThrowsException(
            SutProvider<FeatureRoutedCacheService> sutProvider,
            Guid organizationId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            .Returns(false);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutProvider.Sut.BaseDeleteOrganizationAbilityAsync(organizationId));

        // Assert
        Assert.Equal(
            ExpectedErrorMessage,
            ex.Message);
    }
}
