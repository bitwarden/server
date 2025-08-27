using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;
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

        await sutProvider.GetDependency<IApplicationCacheService>()
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
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilitiesAsync();

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IApplicationCacheService>()
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

        await sutProvider.GetDependency<IApplicationCacheService>()
            .DidNotReceive()
            .GetOrganizationAbilitiesAsync();
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

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(orgId);

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IApplicationCacheService>()
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

        await sutProvider.GetDependency<IApplicationCacheService>()
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
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IApplicationCacheService>()
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

        await sutProvider.GetDependency<IApplicationCacheService>()
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
        await sutProvider.GetDependency<IApplicationCacheService>()
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

        await sutProvider.GetDependency<IApplicationCacheService>()
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
        await sutProvider.GetDependency<IApplicationCacheService>()
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

        await sutProvider.GetDependency<IApplicationCacheService>()
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
        await sutProvider.GetDependency<IApplicationCacheService>()
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

        await sutProvider.GetDependency<IApplicationCacheService>()
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
        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .DeleteProviderAbilityAsync(providerId);

        await sutProvider.GetDependency<IVNextInMemoryApplicationCacheService>()
            .DidNotReceive()
            .DeleteProviderAbilityAsync(providerId);
    }
}
