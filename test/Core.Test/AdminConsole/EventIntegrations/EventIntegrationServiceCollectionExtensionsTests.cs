using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Services.NoopImplementations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.AdminConsole.EventIntegrations;

public class EventIntegrationServiceCollectionExtensionsTests
{
    private readonly IServiceCollection _services;
    private readonly GlobalSettings _globalSettings;

    public EventIntegrationServiceCollectionExtensionsTests()
    {
        _services = new ServiceCollection();
        _globalSettings = CreateGlobalSettings([]);

        // Add required infrastructure services
        _services.TryAddSingleton(_globalSettings);
        _services.TryAddSingleton<IGlobalSettings>(_globalSettings);
        _services.AddLogging();

        // Mock Redis connection for cache
        _services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

        // Mock required repository dependencies for commands
        _services.TryAddScoped(_ => Substitute.For<IOrganizationIntegrationRepository>());
        _services.TryAddScoped(_ => Substitute.For<IOrganizationIntegrationConfigurationRepository>());
        _services.TryAddScoped(_ => Substitute.For<IOrganizationRepository>());
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_RegistersAllServices()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);

        using var provider = _services.BuildServiceProvider();

        var cache = provider.GetRequiredKeyedService<IFusionCache>(EventIntegrationsCacheConstants.CacheName);
        Assert.NotNull(cache);

        var validator = provider.GetRequiredService<IOrganizationIntegrationConfigurationValidator>();
        Assert.NotNull(validator);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetService<ICreateOrganizationIntegrationCommand>());
        Assert.NotNull(sp.GetService<IUpdateOrganizationIntegrationCommand>());
        Assert.NotNull(sp.GetService<IDeleteOrganizationIntegrationCommand>());
        Assert.NotNull(sp.GetService<IGetOrganizationIntegrationsQuery>());

        Assert.NotNull(sp.GetService<ICreateOrganizationIntegrationConfigurationCommand>());
        Assert.NotNull(sp.GetService<IUpdateOrganizationIntegrationConfigurationCommand>());
        Assert.NotNull(sp.GetService<IDeleteOrganizationIntegrationConfigurationCommand>());
        Assert.NotNull(sp.GetService<IGetOrganizationIntegrationConfigurationsQuery>());
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_CommandsQueries_AreRegisteredAsScoped()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);

        var createIntegrationDescriptor = _services.First(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationCommand));
        var createConfigDescriptor = _services.First(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationConfigurationCommand));

        Assert.Equal(ServiceLifetime.Scoped, createIntegrationDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, createConfigDescriptor.Lifetime);
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_CommandsQueries_DifferentInstancesPerScope()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);

        var provider = _services.BuildServiceProvider();

        ICreateOrganizationIntegrationCommand? instance1, instance2, instance3;
        using (var scope1 = provider.CreateScope())
        {
            instance1 = scope1.ServiceProvider.GetService<ICreateOrganizationIntegrationCommand>();
        }
        using (var scope2 = provider.CreateScope())
        {
            instance2 = scope2.ServiceProvider.GetService<ICreateOrganizationIntegrationCommand>();
        }
        using (var scope3 = provider.CreateScope())
        {
            instance3 = scope3.ServiceProvider.GetService<ICreateOrganizationIntegrationCommand>();
        }

        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.NotNull(instance3);
        Assert.NotSame(instance1, instance2);
        Assert.NotSame(instance2, instance3);
        Assert.NotSame(instance1, instance3);
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_CommandsQueries__SameInstanceWithinScope()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);
        var provider = _services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var instance1 = scope.ServiceProvider.GetService<ICreateOrganizationIntegrationCommand>();
        var instance2 = scope.ServiceProvider.GetService<ICreateOrganizationIntegrationCommand>();

        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_MultipleCalls_IsIdempotent()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);

        var createConfigCmdDescriptors = _services.Where(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationConfigurationCommand)).ToList();
        Assert.Single(createConfigCmdDescriptors);

        var updateIntegrationCmdDescriptors = _services.Where(s =>
            s.ServiceType == typeof(IUpdateOrganizationIntegrationCommand)).ToList();
        Assert.Single(updateIntegrationCmdDescriptors);
    }

    [Fact]
    public void AddOrganizationIntegrationCommandsQueries_RegistersAllIntegrationServices()
    {
        _services.AddOrganizationIntegrationCommandsQueries();

        Assert.Contains(_services, s => s.ServiceType == typeof(ICreateOrganizationIntegrationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IUpdateOrganizationIntegrationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IDeleteOrganizationIntegrationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IGetOrganizationIntegrationsQuery));
    }

    [Fact]
    public void AddOrganizationIntegrationCommandsQueries_MultipleCalls_IsIdempotent()
    {
        _services.AddOrganizationIntegrationCommandsQueries();
        _services.AddOrganizationIntegrationCommandsQueries();
        _services.AddOrganizationIntegrationCommandsQueries();

        var createCmdDescriptors = _services.Where(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationCommand)).ToList();
        Assert.Single(createCmdDescriptors);
    }

    [Fact]
    public void AddOrganizationIntegrationConfigurationCommandsQueries_RegistersAllConfigurationServices()
    {
        _services.AddOrganizationIntegrationConfigurationCommandsQueries();

        Assert.Contains(_services, s => s.ServiceType == typeof(ICreateOrganizationIntegrationConfigurationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IUpdateOrganizationIntegrationConfigurationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IDeleteOrganizationIntegrationConfigurationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IGetOrganizationIntegrationConfigurationsQuery));
    }

    [Fact]
    public void AddOrganizationIntegrationConfigurationCommandsQueries_MultipleCalls_IsIdempotent()
    {
        _services.AddOrganizationIntegrationConfigurationCommandsQueries();
        _services.AddOrganizationIntegrationConfigurationCommandsQueries();
        _services.AddOrganizationIntegrationConfigurationCommandsQueries();

        var createCmdDescriptors = _services.Where(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationConfigurationCommand)).ToList();
        Assert.Single(createCmdDescriptors);
    }

    [Fact]
    public void IsRabbitMqEnabled_AllSettingsPresent_ReturnsTrue()
    {
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange"
        });

        Assert.True(EventIntegrationsServiceCollectionExtensions.IsRabbitMqEnabled(globalSettings));
    }

    [Fact]
    public void IsRabbitMqEnabled_MissingHostName_ReturnsFalse()
    {
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = null,
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange"
        });

        Assert.False(EventIntegrationsServiceCollectionExtensions.IsRabbitMqEnabled(globalSettings));
    }

    [Fact]
    public void IsRabbitMqEnabled_MissingUsername_ReturnsFalse()
    {
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = null,
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange"
        });

        Assert.False(EventIntegrationsServiceCollectionExtensions.IsRabbitMqEnabled(globalSettings));
    }

    [Fact]
    public void IsRabbitMqEnabled_MissingPassword_ReturnsFalse()
    {
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = null,
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange"
        });

        Assert.False(EventIntegrationsServiceCollectionExtensions.IsRabbitMqEnabled(globalSettings));
    }

    [Fact]
    public void IsRabbitMqEnabled_MissingExchangeName_ReturnsFalse()
    {
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = null
        });

        Assert.False(EventIntegrationsServiceCollectionExtensions.IsRabbitMqEnabled(globalSettings));
    }

    [Fact]
    public void IsAzureServiceBusEnabled_AllSettingsPresent_ReturnsTrue()
    {
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:AzureServiceBus:ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            ["GlobalSettings:EventLogging:AzureServiceBus:EventTopicName"] = "events"
        });

        Assert.True(EventIntegrationsServiceCollectionExtensions.IsAzureServiceBusEnabled(globalSettings));
    }

    [Fact]
    public void IsAzureServiceBusEnabled_MissingConnectionString_ReturnsFalse()
    {
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:AzureServiceBus:ConnectionString"] = null,
            ["GlobalSettings:EventLogging:AzureServiceBus:EventTopicName"] = "events"
        });

        Assert.False(EventIntegrationsServiceCollectionExtensions.IsAzureServiceBusEnabled(globalSettings));
    }

    [Fact]
    public void IsAzureServiceBusEnabled_MissingTopicName_ReturnsFalse()
    {
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:AzureServiceBus:ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            ["GlobalSettings:EventLogging:AzureServiceBus:EventTopicName"] = null
        });

        Assert.False(EventIntegrationsServiceCollectionExtensions.IsAzureServiceBusEnabled(globalSettings));
    }

    [Fact]
    public void AddSlackService_AllSettingsPresent_RegistersSlackService()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:Slack:ClientId"] = "test-client-id",
            ["GlobalSettings:Slack:ClientSecret"] = "test-client-secret",
            ["GlobalSettings:Slack:Scopes"] = "test-scopes"
        });

        services.TryAddSingleton(globalSettings);
        services.AddLogging();
        services.AddSlackService(globalSettings);

        var provider = services.BuildServiceProvider();
        var slackService = provider.GetService<ISlackService>();

        Assert.NotNull(slackService);
        Assert.IsType<SlackService>(slackService);

        var httpClientDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IHttpClientFactory));
        Assert.NotNull(httpClientDescriptor);
    }

    [Fact]
    public void AddSlackService_SettingsMissing_RegistersNoopService()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:Slack:ClientId"] = null,
            ["GlobalSettings:Slack:ClientSecret"] = null,
            ["GlobalSettings:Slack:Scopes"] = null
        });

        services.AddSlackService(globalSettings);

        var provider = services.BuildServiceProvider();
        var slackService = provider.GetService<ISlackService>();

        Assert.NotNull(slackService);
        Assert.IsType<NoopSlackService>(slackService);
    }

    [Fact]
    public void AddTeamsService_AllSettingsPresent_RegistersTeamsServices()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:Teams:ClientId"] = "test-client-id",
            ["GlobalSettings:Teams:ClientSecret"] = "test-client-secret",
            ["GlobalSettings:Teams:Scopes"] = "test-scopes"
        });

        services.TryAddSingleton(globalSettings);
        services.AddLogging();
        services.TryAddScoped(_ => Substitute.For<IOrganizationIntegrationRepository>());
        services.AddTeamsService(globalSettings);

        var provider = services.BuildServiceProvider();

        var teamsService = provider.GetService<ITeamsService>();
        Assert.NotNull(teamsService);
        Assert.IsType<TeamsService>(teamsService);

        var bot = provider.GetService<IBot>();
        Assert.NotNull(bot);
        Assert.IsType<TeamsService>(bot);

        var adapter = provider.GetService<IBotFrameworkHttpAdapter>();
        Assert.NotNull(adapter);
        Assert.IsType<BotFrameworkHttpAdapter>(adapter);

        var httpClientDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IHttpClientFactory));
        Assert.NotNull(httpClientDescriptor);
    }

    [Fact]
    public void AddTeamsService_SettingsMissing_RegistersNoopService()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:Teams:ClientId"] = null,
            ["GlobalSettings:Teams:ClientSecret"] = null,
            ["GlobalSettings:Teams:Scopes"] = null
        });

        services.AddTeamsService(globalSettings);

        var provider = services.BuildServiceProvider();
        var teamsService = provider.GetService<ITeamsService>();

        Assert.NotNull(teamsService);
        Assert.IsType<NoopTeamsService>(teamsService);
    }

    [Fact]
    public void AddRabbitMqIntegration_RegistersEventIntegrationHandler()
    {
        var services = new ServiceCollection();
        var listenerConfig = new TestListenerConfiguration();

        // Add required dependencies
        services.TryAddSingleton(Substitute.For<IEventIntegrationPublisher>());
        services.TryAddSingleton(Substitute.For<IIntegrationFilterService>());
        services.TryAddKeyedSingleton(EventIntegrationsCacheConstants.CacheName, Substitute.For<IFusionCache>());
        services.TryAddSingleton(Substitute.For<IOrganizationIntegrationConfigurationRepository>());
        services.TryAddSingleton(Substitute.For<IGroupRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationUserRepository>());
        services.AddLogging();

        services.AddRabbitMqIntegration<WebhookIntegrationConfigurationDetails, TestListenerConfiguration>(listenerConfig);

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredKeyedService<IEventMessageHandler>(listenerConfig.RoutingKey);

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddRabbitMqIntegration_RegistersEventListenerService()
    {
        var services = new ServiceCollection();
        var listenerConfig = new TestListenerConfiguration();

        // Add required dependencies
        services.TryAddSingleton(Substitute.For<IEventIntegrationPublisher>());
        services.TryAddSingleton(Substitute.For<IIntegrationFilterService>());
        services.TryAddKeyedSingleton(EventIntegrationsCacheConstants.CacheName, Substitute.For<IFusionCache>());
        services.TryAddSingleton(Substitute.For<IOrganizationIntegrationConfigurationRepository>());
        services.TryAddSingleton(Substitute.For<IGroupRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationUserRepository>());
        services.TryAddSingleton(Substitute.For<IRabbitMqService>());
        services.AddLogging();

        var beforeCount = services.Count(s => s.ServiceType == typeof(IHostedService));
        services.AddRabbitMqIntegration<WebhookIntegrationConfigurationDetails, TestListenerConfiguration>(listenerConfig);
        var afterCount = services.Count(s => s.ServiceType == typeof(IHostedService));

        // AddRabbitMqIntegration should register 2 hosted services (Event + Integration listeners)
        Assert.Equal(2, afterCount - beforeCount);
    }

    [Fact]
    public void AddRabbitMqIntegration_RegistersIntegrationListenerService()
    {
        var services = new ServiceCollection();
        var listenerConfig = new TestListenerConfiguration();

        // Add required dependencies
        services.TryAddSingleton(Substitute.For<IEventIntegrationPublisher>());
        services.TryAddSingleton(Substitute.For<IIntegrationFilterService>());
        services.TryAddKeyedSingleton(EventIntegrationsCacheConstants.CacheName, Substitute.For<IFusionCache>());
        services.TryAddSingleton(Substitute.For<IOrganizationIntegrationConfigurationRepository>());
        services.TryAddSingleton(Substitute.For<IGroupRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationUserRepository>());
        services.TryAddSingleton(Substitute.For<IRabbitMqService>());
        services.TryAddSingleton(Substitute.For<IIntegrationHandler<WebhookIntegrationConfigurationDetails>>());
        services.TryAddSingleton(TimeProvider.System);
        services.AddLogging();

        var beforeCount = services.Count(s => s.ServiceType == typeof(IHostedService));
        services.AddRabbitMqIntegration<WebhookIntegrationConfigurationDetails, TestListenerConfiguration>(listenerConfig);
        var afterCount = services.Count(s => s.ServiceType == typeof(IHostedService));

        // AddRabbitMqIntegration should register 2 hosted services (Event + Integration listeners)
        Assert.Equal(2, afterCount - beforeCount);
    }

    [Fact]
    public void AddAzureServiceBusIntegration_RegistersEventIntegrationHandler()
    {
        var services = new ServiceCollection();
        var listenerConfig = new TestListenerConfiguration();

        // Add required dependencies
        services.TryAddSingleton(Substitute.For<IEventIntegrationPublisher>());
        services.TryAddSingleton(Substitute.For<IIntegrationFilterService>());
        services.TryAddKeyedSingleton(EventIntegrationsCacheConstants.CacheName, Substitute.For<IFusionCache>());
        services.TryAddSingleton(Substitute.For<IOrganizationIntegrationConfigurationRepository>());
        services.TryAddSingleton(Substitute.For<IGroupRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationUserRepository>());
        services.AddLogging();

        services.AddAzureServiceBusIntegration<WebhookIntegrationConfigurationDetails, TestListenerConfiguration>(listenerConfig);

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredKeyedService<IEventMessageHandler>(listenerConfig.RoutingKey);

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddAzureServiceBusIntegration_RegistersEventListenerService()
    {
        var services = new ServiceCollection();
        var listenerConfig = new TestListenerConfiguration();

        // Add required dependencies
        services.TryAddSingleton(Substitute.For<IEventIntegrationPublisher>());
        services.TryAddSingleton(Substitute.For<IIntegrationFilterService>());
        services.TryAddKeyedSingleton(EventIntegrationsCacheConstants.CacheName, Substitute.For<IFusionCache>());
        services.TryAddSingleton(Substitute.For<IOrganizationIntegrationConfigurationRepository>());
        services.TryAddSingleton(Substitute.For<IGroupRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationUserRepository>());
        services.TryAddSingleton(Substitute.For<IAzureServiceBusService>());
        services.AddLogging();

        var beforeCount = services.Count(s => s.ServiceType == typeof(IHostedService));
        services.AddAzureServiceBusIntegration<WebhookIntegrationConfigurationDetails, TestListenerConfiguration>(listenerConfig);
        var afterCount = services.Count(s => s.ServiceType == typeof(IHostedService));

        // AddAzureServiceBusIntegration should register 2 hosted services (Event + Integration listeners)
        Assert.Equal(2, afterCount - beforeCount);
    }

    [Fact]
    public void AddAzureServiceBusIntegration_RegistersIntegrationListenerService()
    {
        var services = new ServiceCollection();
        var listenerConfig = new TestListenerConfiguration();

        // Add required dependencies
        services.TryAddSingleton(Substitute.For<IEventIntegrationPublisher>());
        services.TryAddSingleton(Substitute.For<IIntegrationFilterService>());
        services.TryAddKeyedSingleton(EventIntegrationsCacheConstants.CacheName, Substitute.For<IFusionCache>());
        services.TryAddSingleton(Substitute.For<IOrganizationIntegrationConfigurationRepository>());
        services.TryAddSingleton(Substitute.For<IGroupRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationRepository>());
        services.TryAddSingleton(Substitute.For<IOrganizationUserRepository>());
        services.TryAddSingleton(Substitute.For<IAzureServiceBusService>());
        services.TryAddSingleton(Substitute.For<IIntegrationHandler<WebhookIntegrationConfigurationDetails>>());
        services.AddLogging();

        var beforeCount = services.Count(s => s.ServiceType == typeof(IHostedService));
        services.AddAzureServiceBusIntegration<WebhookIntegrationConfigurationDetails, TestListenerConfiguration>(listenerConfig);
        var afterCount = services.Count(s => s.ServiceType == typeof(IHostedService));

        // AddAzureServiceBusIntegration should register 2 hosted services (Event + Integration listeners)
        Assert.Equal(2, afterCount - beforeCount);
    }

    [Fact]
    public void AddEventIntegrationServices_RegistersCommonServices()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings([]);

        // Add prerequisites
        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddLogging();

        services.AddEventIntegrationServices(globalSettings);

        // Verify common services are registered
        Assert.Contains(services, s => s.ServiceType == typeof(IIntegrationFilterService));
        Assert.Contains(services, s => s.ServiceType == typeof(TimeProvider));

        // Verify HttpClients for handlers are registered
        var httpClientDescriptors = services.Where(s => s.ServiceType == typeof(IHttpClientFactory)).ToList();
        Assert.NotEmpty(httpClientDescriptors);
    }

    [Fact]
    public void AddEventIntegrationServices_RegistersIntegrationHandlers()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings([]);

        // Add prerequisites
        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddLogging();

        services.AddEventIntegrationServices(globalSettings);

        // Verify integration handlers are registered
        Assert.Contains(services, s => s.ServiceType == typeof(IIntegrationHandler<SlackIntegrationConfigurationDetails>));
        Assert.Contains(services, s => s.ServiceType == typeof(IIntegrationHandler<WebhookIntegrationConfigurationDetails>));
        Assert.Contains(services, s => s.ServiceType == typeof(IIntegrationHandler<DatadogIntegrationConfigurationDetails>));
        Assert.Contains(services, s => s.ServiceType == typeof(IIntegrationHandler<TeamsIntegrationConfigurationDetails>));
    }

    [Fact]
    public void AddEventIntegrationServices_RabbitMqEnabled_RegistersRabbitMqListeners()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange"
        });

        // Add prerequisites
        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddLogging();

        var beforeCount = services.Count(s => s.ServiceType == typeof(IHostedService));
        services.AddEventIntegrationServices(globalSettings);
        var afterCount = services.Count(s => s.ServiceType == typeof(IHostedService));

        // Should register 11 hosted services for RabbitMQ: 1 repository + 5*2 integration listeners (event+integration)
        Assert.Equal(11, afterCount - beforeCount);
    }

    [Fact]
    public void AddEventIntegrationServices_AzureServiceBusEnabled_RegistersAzureListeners()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:AzureServiceBus:ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            ["GlobalSettings:EventLogging:AzureServiceBus:EventTopicName"] = "events"
        });

        // Add prerequisites
        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddLogging();

        var beforeCount = services.Count(s => s.ServiceType == typeof(IHostedService));
        services.AddEventIntegrationServices(globalSettings);
        var afterCount = services.Count(s => s.ServiceType == typeof(IHostedService));

        // Should register 11 hosted services for Azure Service Bus: 1 repository + 5*2 integration listeners (event+integration)
        Assert.Equal(11, afterCount - beforeCount);
    }

    [Fact]
    public void AddEventIntegrationServices_BothEnabled_AzureServiceBusTakesPrecedence()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange",
            ["GlobalSettings:EventLogging:AzureServiceBus:ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            ["GlobalSettings:EventLogging:AzureServiceBus:EventTopicName"] = "events"
        });

        // Add prerequisites
        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddLogging();

        var beforeCount = services.Count(s => s.ServiceType == typeof(IHostedService));
        services.AddEventIntegrationServices(globalSettings);
        var afterCount = services.Count(s => s.ServiceType == typeof(IHostedService));

        // Should register 11 hosted services for Azure Service Bus: 1 repository + 5*2 integration listeners (event+integration)
        // NO RabbitMQ services should be enabled because ASB takes precedence
        Assert.Equal(11, afterCount - beforeCount);
    }

    [Fact]
    public void AddEventIntegrationServices_NeitherEnabled_RegistersNoListeners()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings([]);

        // Add prerequisites
        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddLogging();

        var beforeCount = services.Count(s => s.ServiceType == typeof(IHostedService));
        services.AddEventIntegrationServices(globalSettings);
        var afterCount = services.Count(s => s.ServiceType == typeof(IHostedService));

        // Should register no hosted services when neither RabbitMQ nor Azure Service Bus is enabled
        Assert.Equal(0, afterCount - beforeCount);
    }

    [Fact]
    public void AddEventWriteServices_AzureServiceBusEnabled_RegistersAzureServices()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:AzureServiceBus:ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            ["GlobalSettings:EventLogging:AzureServiceBus:EventTopicName"] = "events"
        });

        services.AddEventWriteServices(globalSettings);

        Assert.Contains(services, s => s.ServiceType == typeof(IEventIntegrationPublisher) && s.ImplementationType == typeof(AzureServiceBusService));
        Assert.Contains(services, s => s.ServiceType == typeof(IEventWriteService) && s.ImplementationType == typeof(EventIntegrationEventWriteService));
    }

    [Fact]
    public void AddEventWriteServices_RabbitMqEnabled_RegistersRabbitMqServices()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange"
        });

        services.AddEventWriteServices(globalSettings);

        Assert.Contains(services, s => s.ServiceType == typeof(IEventIntegrationPublisher) && s.ImplementationType == typeof(RabbitMqService));
        Assert.Contains(services, s => s.ServiceType == typeof(IEventWriteService) && s.ImplementationType == typeof(EventIntegrationEventWriteService));
    }

    [Fact]
    public void AddEventWriteServices_EventsConnectionStringPresent_RegistersAzureQueueService()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:Events:ConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net",
            ["GlobalSettings:Events:QueueName"] = "event"
        });

        services.AddEventWriteServices(globalSettings);

        Assert.Contains(services, s => s.ServiceType == typeof(IEventWriteService) && s.ImplementationType == typeof(AzureQueueEventWriteService));
    }

    [Fact]
    public void AddEventWriteServices_SelfHosted_RegistersRepositoryService()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:SelfHosted"] = "true"
        });

        services.AddEventWriteServices(globalSettings);

        Assert.Contains(services, s => s.ServiceType == typeof(IEventWriteService) && s.ImplementationType == typeof(RepositoryEventWriteService));
    }

    [Fact]
    public void AddEventWriteServices_NothingEnabled_RegistersNoopService()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings([]);

        services.AddEventWriteServices(globalSettings);

        Assert.Contains(services, s => s.ServiceType == typeof(IEventWriteService) && s.ImplementationType == typeof(NoopEventWriteService));
    }

    [Fact]
    public void AddEventWriteServices_AzureTakesPrecedenceOverRabbitMq()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:AzureServiceBus:ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            ["GlobalSettings:EventLogging:AzureServiceBus:EventTopicName"] = "events",
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange"
        });

        services.AddEventWriteServices(globalSettings);

        // Should use Azure Service Bus, not RabbitMQ
        Assert.Contains(services, s => s.ServiceType == typeof(IEventIntegrationPublisher) && s.ImplementationType == typeof(AzureServiceBusService));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IEventIntegrationPublisher) && s.ImplementationType == typeof(RabbitMqService));
    }

    [Fact]
    public void AddAzureServiceBusListeners_AzureServiceBusEnabled_RegistersAzureServiceBusServices()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:AzureServiceBus:ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            ["GlobalSettings:EventLogging:AzureServiceBus:EventTopicName"] = "events"
        });

        // Add prerequisites
        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddLogging();

        services.AddAzureServiceBusListeners(globalSettings);

        Assert.Contains(services, s => s.ServiceType == typeof(IAzureServiceBusService));
        Assert.Contains(services, s => s.ServiceType == typeof(IEventRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(AzureTableStorageEventHandler));
    }

    [Fact]
    public void AddAzureServiceBusListeners_AzureServiceBusDisabled_ReturnsEarly()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings([]);

        var initialCount = services.Count;
        services.AddAzureServiceBusListeners(globalSettings);
        var finalCount = services.Count;

        Assert.Equal(initialCount, finalCount);
    }

    [Fact]
    public void AddRabbitMqListeners_RabbitMqEnabled_RegistersRabbitMqServices()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            ["GlobalSettings:EventLogging:RabbitMq:HostName"] = "localhost",
            ["GlobalSettings:EventLogging:RabbitMq:Username"] = "user",
            ["GlobalSettings:EventLogging:RabbitMq:Password"] = "pass",
            ["GlobalSettings:EventLogging:RabbitMq:EventExchangeName"] = "exchange"
        });

        // Add prerequisites
        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddLogging();

        services.AddRabbitMqListeners(globalSettings);

        Assert.Contains(services, s => s.ServiceType == typeof(IRabbitMqService));
        Assert.Contains(services, s => s.ServiceType == typeof(EventRepositoryHandler));
    }

    [Fact]
    public void AddRabbitMqListeners_RabbitMqDisabled_ReturnsEarly()
    {
        var services = new ServiceCollection();
        var globalSettings = CreateGlobalSettings([]);

        var initialCount = services.Count;
        services.AddRabbitMqListeners(globalSettings);
        var finalCount = services.Count;

        Assert.Equal(initialCount, finalCount);
    }

    private static GlobalSettings CreateGlobalSettings(Dictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        var settings = new GlobalSettings();
        config.GetSection("GlobalSettings").Bind(settings);
        return settings;
    }
}
