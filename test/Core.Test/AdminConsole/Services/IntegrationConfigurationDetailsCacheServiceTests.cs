#nullable enable

using System.Text.Json;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class IntegrationConfigurationDetailsCacheServiceTests
{
    private SutProvider<IntegrationConfigurationDetailsCacheService> GetSutProvider(
        List<OrganizationIntegrationConfigurationDetails> configurations)
    {
        var configurationRepository = Substitute.For<IOrganizationIntegrationConfigurationRepository>();
        configurationRepository.GetAllConfigurationDetailsAsync().Returns(configurations);

        return new SutProvider<IntegrationConfigurationDetailsCacheService>()
            .SetDependency(configurationRepository)
            .Create();
    }

    [Theory, BitAutoData]
    public async Task GetConfigurationDetails_KeyExists_ReturnsExpectedList(OrganizationIntegrationConfigurationDetails config)
    {
        var sutProvider = GetSutProvider([config]);
        await sutProvider.Sut.RefreshAsync();
        var result = sutProvider.Sut.GetConfigurationDetails(
            config.OrganizationId,
            config.IntegrationType,
            config.EventType);
        Assert.Single(result);
        Assert.Same(config, result[0]);
    }

    [Theory, BitAutoData]
    public async Task GetConfigurationDetails_KeyMissing_ReturnsEmptyList(OrganizationIntegrationConfigurationDetails config)
    {
        var sutProvider = GetSutProvider([config]);
        await sutProvider.Sut.RefreshAsync();
        var result = sutProvider.Sut.GetConfigurationDetails(
            Guid.NewGuid(),
            config.IntegrationType,
            config.EventType);
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetConfigurationDetails_ReturnsCachedValue_EvenIfRepositoryChanges(OrganizationIntegrationConfigurationDetails config)
    {
        var sutProvider = GetSutProvider([config]);
        await sutProvider.Sut.RefreshAsync();

        var newConfig = JsonSerializer.Deserialize<OrganizationIntegrationConfigurationDetails>(JsonSerializer.Serialize(config));
        Assert.NotNull(newConfig);
        newConfig.Template = "Changed";
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().GetAllConfigurationDetailsAsync()
            .Returns([newConfig]);

        var result = sutProvider.Sut.GetConfigurationDetails(
            config.OrganizationId,
            config.IntegrationType,
            config.EventType);
        Assert.Single(result);
        Assert.NotEqual("Changed", result[0].Template); // should not yet pick up change from repository

        await sutProvider.Sut.RefreshAsync();  // Pick up changes

        result = sutProvider.Sut.GetConfigurationDetails(
            config.OrganizationId,
            config.IntegrationType,
            config.EventType);
        Assert.Single(result);
        Assert.Equal("Changed", result[0].Template); // Should have the new value
    }

    [Theory, BitAutoData]
    public async Task RefreshAsync_GroupsByCompositeKey(OrganizationIntegrationConfigurationDetails config1)
    {
        var config2 = JsonSerializer.Deserialize<OrganizationIntegrationConfigurationDetails>(
            JsonSerializer.Serialize(config1))!;
        config2.Template = "Another";

        var sutProvider = GetSutProvider([config1, config2]);
        await sutProvider.Sut.RefreshAsync();

        var results = sutProvider.Sut.GetConfigurationDetails(
            config1.OrganizationId,
            config1.IntegrationType,
            config1.EventType);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Template == config1.Template);
        Assert.Contains(results, r => r.Template == config2.Template);
    }

    [Theory, BitAutoData]
    public async Task RefreshAsync_LogsInformationOnSuccess(OrganizationIntegrationConfigurationDetails config)
    {
        var sutProvider = GetSutProvider([config]);
        await sutProvider.Sut.RefreshAsync();

        sutProvider.GetDependency<ILogger<IntegrationConfigurationDetailsCacheService>>().Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Refreshed successfully")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RefreshAsync_OnException_LogsError()
    {
        var sutProvider = GetSutProvider([]);
        sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().GetAllConfigurationDetailsAsync()
            .Throws(new Exception("Database failure"));
        await sutProvider.Sut.RefreshAsync();

        sutProvider.GetDependency<ILogger<IntegrationConfigurationDetailsCacheService>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Refresh failed")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
