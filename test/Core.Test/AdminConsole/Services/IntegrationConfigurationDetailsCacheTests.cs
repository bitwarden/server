#nullable enable

using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class IntegrationConfigurationDetailsCacheTests
{
    private const string _template = "Template";
    private static readonly Uri _uri = new Uri("https://example.com");

    private SutProvider<IntegrationConfigurationDetailsCache> GetSutProvider(
        List<OrganizationIntegrationConfigurationDetails> configurations)
    {
        var configurationRepository = Substitute.For<IOrganizationIntegrationConfigurationRepository>();
        configurationRepository.GetConfigurationDetailsAsync(Arg.Any<Guid>(),
            IntegrationType.Webhook, Arg.Any<EventType>()).Returns(configurations);

        return new SutProvider<IntegrationConfigurationDetailsCache>()
            .SetDependency(configurationRepository)
            .Create();
    }

    [Theory, BitAutoData]
    public async Task GetOrAddAsync_NoCachedOrFetched_ReturnsEmpty(Guid organizationId, EventType eventType)
    {
        var sutProvider = GetSutProvider(NoConfigurations());
        var result = await sutProvider.Sut.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(
            organizationId,
            IntegrationType.Webhook,
            eventType);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetOrAddAsync_NoCachedOneFetched_ReturnsParsedConfiguration(Guid organizationId, EventType eventType)
    {
        var sutProvider = GetSutProvider(OneConfiguration());
        var result = await sutProvider.Sut.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(
            organizationId,
            IntegrationType.Webhook,
            eventType);

        Assert.Single(result);
        Assert.Equal(_uri, result[0].Configuration.Uri);
        Assert.Equal(_template, result[0].Template);
    }

    [Theory, BitAutoData]
    public async Task GetOrAddAsync_OneCachedNoneFetched_ReturnsCachedWithoutFetch(Guid organizationId, EventType eventType)
    {
        var sutProvider = GetSutProvider(NoConfigurations());
        var cached = new List<CachedIntegrationConfigurationDetails<WebhookIntegrationConfigurationDetails>>
        {
            new()
            {
                FilterGroup = null,
                Configuration = new WebhookIntegrationConfigurationDetails(Uri: _uri),
                Template = _template
            }
        };

        object boxed = cached;
        sutProvider.GetDependency<IMemoryCache>().TryGetValue(Arg.Any<string>(), out Arg.Any<object>()!)
            .Returns(ci =>
            {
                ci[1] = boxed;
                return true;
            });

        var result = await sutProvider.Sut.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(
            organizationId,
            IntegrationType.Webhook,
            eventType);

        Assert.Single(result);
        Assert.Equal(_uri, result[0].Configuration.Uri);
        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>()
            .DidNotReceiveWithAnyArgs().GetConfigurationDetailsAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task GetOrAddAsync_InvalidConfiguration_LogsErrorAndReturnsEmpty(Guid organizationId, EventType eventType)
    {
        var sutProvider = GetSutProvider(InvalidConfiguration());
        var result = await sutProvider.Sut.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(
            organizationId,
            IntegrationType.Webhook,
            eventType);

        sutProvider
            .GetDependency<ILogger<IntegrationConfigurationDetailsCache>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<JsonException>(),
                Arg.Any<Func<object, Exception, string>>()!);

        Assert.Empty(result);
    }
    [Theory, BitAutoData]
    public async Task GetOrAddAsync_InvalidFilter_LogsErrorAndReturnsEmpty(Guid organizationId, EventType eventType)
    {
        var sutProvider = GetSutProvider(InvalidFilterConfiguration());
        var result = await sutProvider.Sut.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(
            organizationId,
            IntegrationType.Webhook,
            eventType);

        sutProvider
            .GetDependency<ILogger<IntegrationConfigurationDetailsCache>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<JsonException>(),
                Arg.Any<Func<object, Exception, string>>()!);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetOrAddAsync_ValidFilter_ParsesFilterGroup(Guid organizationId, EventType eventType)
    {
        var sutProvider = GetSutProvider(ValidFilterConfiguration());
        var result = await sutProvider.Sut.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(
            organizationId,
            IntegrationType.Webhook,
            eventType);

        Assert.Single(result);
        Assert.NotNull(result[0].FilterGroup);
        Assert.Equal(_uri, result[0].Configuration.Uri);
        Assert.Equal(_template, result[0].Template);
        Assert.Empty(sutProvider
            .GetDependency<ILogger<IntegrationConfigurationDetailsCache>>()
            .ReceivedCalls());
    }

    [Theory, BitAutoData]
    public async Task GetOrAddAsync_OneValidOneInvalidConfig_LogsErrorAndReturnsValid(Guid organizationId, EventType eventType)
    {
        var good = OneConfiguration()[0];
        var bad = InvalidConfiguration()[0];
        var sutProvider = GetSutProvider(new() { bad, good });

        var result = await sutProvider.Sut.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(
            organizationId,
            IntegrationType.Webhook,
            eventType);

        Assert.Single(result);
        Assert.Equal(_uri, result[0].Configuration.Uri);
        sutProvider
            .GetDependency<ILogger<IntegrationConfigurationDetailsCache>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<JsonException>(),
                Arg.Any<Func<object, Exception, string>>()!);
    }

    [Theory, BitAutoData]
    public async Task GetOrAddAsync_OneValidOneInvalidFilter_LogsErrorAndReturnsValid(Guid organizationId, EventType eventType)
    {
        var good = ValidFilterConfiguration()[0];
        var bad = InvalidFilterConfiguration()[0];
        var sutProvider = GetSutProvider(new() { bad, good });

        var result = await sutProvider.Sut.GetOrAddAsync<WebhookIntegrationConfigurationDetails>(
            organizationId,
            IntegrationType.Webhook,
            eventType);

        Assert.Single(result);
        Assert.NotNull(result[0].FilterGroup);
        Assert.Equal(_uri, result[0].Configuration.Uri);
        sutProvider
            .GetDependency<ILogger<IntegrationConfigurationDetailsCache>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<JsonException>(),
                Arg.Any<Func<object, Exception, string>>()!);
    }

    [Theory, BitAutoData]
    public void RemoveCacheEntry_RemovesExpectedKey(Guid organizationId, EventType eventType)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        var expectedKey = $"integration-config:{organizationId}:{IntegrationType.Webhook}:{eventType}";

        sutProvider.Sut.RemoveCacheEntry(organizationId, IntegrationType.Webhook, eventType);

        sutProvider
            .GetDependency<IMemoryCache>()
            .Received(1)
            .Remove(expectedKey);
    }

    [Theory, BitAutoData]
    public void RemoveCacheEntriesForIntegration_RemovesAllEventTypes(Guid organizationId)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        sutProvider.Sut.RemoveCacheEntriesForIntegration(organizationId, IntegrationType.Webhook);

        foreach (var eventType in Enum.GetValues<EventType>())
        {
            var expectedKey = $"integration-config:{organizationId}:{IntegrationType.Webhook}:{eventType}";
            sutProvider
                .GetDependency<IMemoryCache>()
                .Received(1)
                .Remove(expectedKey);
        }
    }

    private static List<OrganizationIntegrationConfigurationDetails> NoConfigurations()
    {
        return [];
    }

    private static List<OrganizationIntegrationConfigurationDetails> OneConfiguration()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Uri = _uri });
        config.Template = _template;

        return [config];
    }

    private static List<OrganizationIntegrationConfigurationDetails> InvalidConfiguration()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = "{ Bad json ..";
        config.Template = _template;
        config.Filters = "Invalid Configuration!";

        return [config];
    }

    private static List<OrganizationIntegrationConfigurationDetails> InvalidFilterConfiguration()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Uri = _uri });
        config.Template = _template;
        config.Filters = "Invalid Configuration!";

        return [config];
    }

    private static List<OrganizationIntegrationConfigurationDetails> ValidFilterConfiguration()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = null;
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { Uri = _uri });
        config.Template = _template;
        config.Filters = JsonSerializer.Serialize(new IntegrationFilterGroup() { });

        return [config];
    }
}
