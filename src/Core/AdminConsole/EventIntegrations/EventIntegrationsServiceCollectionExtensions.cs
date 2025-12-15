using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.Models.Teams;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Services.NoopImplementations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using TableStorageRepos = Bit.Core.Repositories.TableStorage;

namespace Microsoft.Extensions.DependencyInjection;

public static class EventIntegrationsServiceCollectionExtensions
{
    /// <summary>
    /// Adds all event integrations commands, queries, and required cache infrastructure.
    /// This method is idempotent and can be called multiple times safely.
    /// </summary>
    public static IServiceCollection AddEventIntegrationsCommandsQueries(
        this IServiceCollection services,
        GlobalSettings globalSettings)
    {
        // Ensure cache is registered first - commands depend on this keyed cache.
        // This is idempotent for the same named cache, so it's safe to call.
        services.AddExtendedCache(EventIntegrationsCacheConstants.CacheName, globalSettings);

        // Add Validator
        services.TryAddSingleton<IOrganizationIntegrationConfigurationValidator, OrganizationIntegrationConfigurationValidator>();

        // Add all commands/queries
        services.AddOrganizationIntegrationCommandsQueries();
        services.AddOrganizationIntegrationConfigurationCommandsQueries();

        return services;
    }

    /// <summary>
    /// Registers event write services based on available configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="globalSettings">The global settings containing event logging configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers the appropriate IEventWriteService implementation based on the available
    /// configuration, checking in the following priority order:
    /// </para>
    /// <para>
    /// 1. Azure Service Bus - If all Azure Service Bus settings are present, registers
    /// EventIntegrationEventWriteService with AzureServiceBusService as the publisher
    /// </para>
    /// <para>
    /// 2. RabbitMQ - If all RabbitMQ settings are present, registers EventIntegrationEventWriteService with
    /// RabbitMqService as the publisher
    /// </para>
    /// <para>
    /// 3. Azure Queue Storage - If Events.ConnectionString is present, registers AzureQueueEventWriteService
    /// </para>
    /// <para>
    /// 4. Repository (Self-Hosted) - If SelfHosted is true, registers RepositoryEventWriteService
    /// </para>
    /// <para>
    /// 5. Noop - If none of the above are configured, registers NoopEventWriteService (no-op implementation)
    /// </para>
    /// </remarks>
    public static IServiceCollection AddEventWriteServices(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (IsAzureServiceBusEnabled(globalSettings))
        {
            services.TryAddSingleton<IEventIntegrationPublisher, AzureServiceBusService>();
            services.TryAddSingleton<IEventWriteService, EventIntegrationEventWriteService>();
            return services;
        }

        if (IsRabbitMqEnabled(globalSettings))
        {
            services.TryAddSingleton<IEventIntegrationPublisher, RabbitMqService>();
            services.TryAddSingleton<IEventWriteService, EventIntegrationEventWriteService>();
            return services;
        }

        if (CoreHelpers.SettingHasValue(globalSettings.Events.ConnectionString) &&
            CoreHelpers.SettingHasValue(globalSettings.Events.QueueName))
        {
            services.TryAddSingleton<IEventWriteService, AzureQueueEventWriteService>();
            return services;
        }

        if (globalSettings.SelfHosted)
        {
            services.TryAddSingleton<IEventWriteService, RepositoryEventWriteService>();
            return services;
        }

        services.TryAddSingleton<IEventWriteService, NoopEventWriteService>();
        return services;
    }

    /// <summary>
    /// Registers Azure Service Bus-based event integration listeners and supporting infrastructure.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="globalSettings">The global settings containing Azure Service Bus configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// If Azure Service Bus is not enabled (missing required settings), this method returns immediately
    /// without registering any services.
    /// </para>
    /// <para>
    /// When Azure Service Bus is enabled, this method registers:
    /// - IAzureServiceBusService and IEventIntegrationPublisher implementations
    /// - Table Storage event repository
    /// - Azure Table Storage event handler
    /// - All event integration services via AddEventIntegrationServices
    /// </para>
    /// <para>
    /// PREREQUISITE: Callers must ensure AddDistributedCache has been called before this method,
    /// as it is required to create the event integrations extended cache.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAzureServiceBusListeners(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (!IsAzureServiceBusEnabled(globalSettings))
        {
            return services;
        }

        services.TryAddSingleton<IAzureServiceBusService, AzureServiceBusService>();
        services.TryAddSingleton<IEventIntegrationPublisher, AzureServiceBusService>();
        services.TryAddSingleton<IEventRepository, TableStorageRepos.EventRepository>();
        services.TryAddKeyedSingleton<IEventWriteService, RepositoryEventWriteService>("persistent");
        services.TryAddSingleton<AzureTableStorageEventHandler>();

        services.AddEventIntegrationServices(globalSettings);

        return services;
    }

    /// <summary>
    /// Registers RabbitMQ-based event integration listeners and supporting infrastructure.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="globalSettings">The global settings containing RabbitMQ configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// If RabbitMQ is not enabled (missing required settings), this method returns immediately
    /// without registering any services.
    /// </para>
    /// <para>
    /// When RabbitMQ is enabled, this method registers:
    /// - IRabbitMqService and IEventIntegrationPublisher implementations
    /// - Event repository handler
    /// - All event integration services via AddEventIntegrationServices
    /// </para>
    /// <para>
    /// PREREQUISITE: Callers must ensure AddDistributedCache has been called before this method,
    /// as it is required to create the event integrations extended cache.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddRabbitMqListeners(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (!IsRabbitMqEnabled(globalSettings))
        {
            return services;
        }

        services.TryAddSingleton<IRabbitMqService, RabbitMqService>();
        services.TryAddSingleton<IEventIntegrationPublisher, RabbitMqService>();
        services.TryAddSingleton<EventRepositoryHandler>();

        services.AddEventIntegrationServices(globalSettings);

        return services;
    }

    /// <summary>
    /// Registers Slack integration services based on configuration settings.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="globalSettings">The global settings containing Slack configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// If all required Slack settings are configured (ClientId, ClientSecret, Scopes), registers the full SlackService,
    /// including an HttpClient for Slack API calls. Otherwise, registers a NoopSlackService that performs no operations.
    /// </remarks>
    public static IServiceCollection AddSlackService(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (CoreHelpers.SettingHasValue(globalSettings.Slack.ClientId) &&
            CoreHelpers.SettingHasValue(globalSettings.Slack.ClientSecret) &&
            CoreHelpers.SettingHasValue(globalSettings.Slack.Scopes))
        {
            services.AddHttpClient(SlackService.HttpClientName);
            services.TryAddSingleton<ISlackService, SlackService>();
        }
        else
        {
            services.TryAddSingleton<ISlackService, NoopSlackService>();
        }

        return services;
    }

    /// <summary>
    /// Registers Microsoft Teams integration services based on configuration settings.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="globalSettings">The global settings containing Teams configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// If all required Teams settings are configured (ClientId, ClientSecret, Scopes), registers:
    /// - TeamsService and its interfaces (IBot, ITeamsService)
    /// - IBotFrameworkHttpAdapter with Teams credentials
    /// - HttpClient for Teams API calls
    /// Otherwise, registers a NoopTeamsService that performs no operations.
    /// </remarks>
    public static IServiceCollection AddTeamsService(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (CoreHelpers.SettingHasValue(globalSettings.Teams.ClientId) &&
            CoreHelpers.SettingHasValue(globalSettings.Teams.ClientSecret) &&
            CoreHelpers.SettingHasValue(globalSettings.Teams.Scopes))
        {
            services.AddHttpClient(TeamsService.HttpClientName);
            services.TryAddSingleton<TeamsService>();
            services.TryAddSingleton<IBot>(sp => sp.GetRequiredService<TeamsService>());
            services.TryAddSingleton<ITeamsService>(sp => sp.GetRequiredService<TeamsService>());
            services.TryAddSingleton<IBotFrameworkHttpAdapter>(_ =>
                new BotFrameworkHttpAdapter(
                    new TeamsBotCredentialProvider(
                        clientId: globalSettings.Teams.ClientId,
                        clientSecret: globalSettings.Teams.ClientSecret
                    )
                )
            );
        }
        else
        {
            services.TryAddSingleton<ITeamsService, NoopTeamsService>();
        }

        return services;
    }

    /// <summary>
    /// Registers event integration services including handlers, listeners, and supporting infrastructure.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="globalSettings">The global settings containing integration configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method orchestrates the registration of all event integration components based on the enabled
    /// message broker (Azure Service Bus or RabbitMQ). It is an internal method called by the public
    /// entry points AddAzureServiceBusListeners and AddRabbitMqListeners.
    /// </para>
    /// <para>
    /// NOTE: If both Azure Service Bus and RabbitMQ are configured, Azure Service Bus takes precedence. This means that
    /// Azure Service Bus listeners will be registered (and RabbitMQ listeners will NOT) even if this event is called
    /// from AddRabbitMqListeners when Azure Service Bus settings are configured.
    /// </para>
    /// <para>
    /// PREREQUISITE: Callers must ensure AddDistributedCache has been called before invoking this method.
    /// This method depends on distributed cache infrastructure being available for the keyed extended
    /// cache registration.
    /// </para>
    /// <para>
    /// Registered Services:
    /// - Keyed ExtendedCache for event integrations
    /// - Integration filter service
    /// - Integration handlers for Slack, Webhook, Hec, Datadog, and Teams
    /// - Hosted services for event and integration listeners (based on enabled message broker)
    /// </para>
    /// </remarks>
    internal static IServiceCollection AddEventIntegrationServices(this IServiceCollection services,
        GlobalSettings globalSettings)
    {
        // Add common services
        // NOTE: AddDistributedCache must be called by the caller before this method
        services.AddExtendedCache(EventIntegrationsCacheConstants.CacheName, globalSettings);
        services.TryAddSingleton<IIntegrationFilterService, IntegrationFilterService>();
        services.TryAddKeyedSingleton<IEventWriteService, RepositoryEventWriteService>("persistent");

        // Add services in support of handlers
        services.AddSlackService(globalSettings);
        services.AddTeamsService(globalSettings);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(WebhookIntegrationHandler.HttpClientName);
        services.AddHttpClient(DatadogIntegrationHandler.HttpClientName);

        // Add integration handlers
        services.TryAddSingleton<IIntegrationHandler<SlackIntegrationConfigurationDetails>, SlackIntegrationHandler>();
        services.TryAddSingleton<IIntegrationHandler<WebhookIntegrationConfigurationDetails>, WebhookIntegrationHandler>();
        services.TryAddSingleton<IIntegrationHandler<DatadogIntegrationConfigurationDetails>, DatadogIntegrationHandler>();
        services.TryAddSingleton<IIntegrationHandler<TeamsIntegrationConfigurationDetails>, TeamsIntegrationHandler>();

        var repositoryConfiguration = new RepositoryListenerConfiguration(globalSettings);
        var slackConfiguration = new SlackListenerConfiguration(globalSettings);
        var webhookConfiguration = new WebhookListenerConfiguration(globalSettings);
        var hecConfiguration = new HecListenerConfiguration(globalSettings);
        var datadogConfiguration = new DatadogListenerConfiguration(globalSettings);
        var teamsConfiguration = new TeamsListenerConfiguration(globalSettings);

        if (IsAzureServiceBusEnabled(globalSettings))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
                    AzureServiceBusEventListenerService<RepositoryListenerConfiguration>>(provider =>
                    new AzureServiceBusEventListenerService<RepositoryListenerConfiguration>(
                        configuration: repositoryConfiguration,
                        handler: provider.GetRequiredService<AzureTableStorageEventHandler>(),
                        serviceBusService: provider.GetRequiredService<IAzureServiceBusService>(),
                        serviceBusOptions: new ServiceBusProcessorOptions()
                        {
                            PrefetchCount = repositoryConfiguration.EventPrefetchCount,
                            MaxConcurrentCalls = repositoryConfiguration.EventMaxConcurrentCalls
                        },
                        loggerFactory: provider.GetRequiredService<ILoggerFactory>()
                    )
                )
            );
            services.AddAzureServiceBusIntegration<SlackIntegrationConfigurationDetails, SlackListenerConfiguration>(slackConfiguration);
            services.AddAzureServiceBusIntegration<WebhookIntegrationConfigurationDetails, WebhookListenerConfiguration>(webhookConfiguration);
            services.AddAzureServiceBusIntegration<WebhookIntegrationConfigurationDetails, HecListenerConfiguration>(hecConfiguration);
            services.AddAzureServiceBusIntegration<DatadogIntegrationConfigurationDetails, DatadogListenerConfiguration>(datadogConfiguration);
            services.AddAzureServiceBusIntegration<TeamsIntegrationConfigurationDetails, TeamsListenerConfiguration>(teamsConfiguration);

            return services;
        }

        if (IsRabbitMqEnabled(globalSettings))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
                    RabbitMqEventListenerService<RepositoryListenerConfiguration>>(provider =>
                    new RabbitMqEventListenerService<RepositoryListenerConfiguration>(
                        handler: provider.GetRequiredService<EventRepositoryHandler>(),
                        configuration: repositoryConfiguration,
                        rabbitMqService: provider.GetRequiredService<IRabbitMqService>(),
                        loggerFactory: provider.GetRequiredService<ILoggerFactory>()
                    )
                )
            );
            services.AddRabbitMqIntegration<SlackIntegrationConfigurationDetails, SlackListenerConfiguration>(slackConfiguration);
            services.AddRabbitMqIntegration<WebhookIntegrationConfigurationDetails, WebhookListenerConfiguration>(webhookConfiguration);
            services.AddRabbitMqIntegration<WebhookIntegrationConfigurationDetails, HecListenerConfiguration>(hecConfiguration);
            services.AddRabbitMqIntegration<DatadogIntegrationConfigurationDetails, DatadogListenerConfiguration>(datadogConfiguration);
            services.AddRabbitMqIntegration<TeamsIntegrationConfigurationDetails, TeamsListenerConfiguration>(teamsConfiguration);
        }

        return services;
    }

    /// <summary>
    /// Registers Azure Service Bus-based event integration listeners for a specific integration type.
    /// </summary>
    /// <typeparam name="TConfig">The integration configuration details type (e.g., SlackIntegrationConfigurationDetails).</typeparam>
    /// <typeparam name="TListenerConfig">The listener configuration type implementing IIntegrationListenerConfiguration.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="listenerConfiguration">The listener configuration containing routing keys and message processing settings.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers three key components:
    /// 1. EventIntegrationHandler - Keyed singleton for processing integration events
    /// 2. AzureServiceBusEventListenerService - Hosted service for listening to event messages from Azure Service Bus
    ///    for this integration type
    /// 3. AzureServiceBusIntegrationListenerService - Hosted service for listening to integration messages from
    ///    Azure Service Bus for this integration type
    /// </para>
    /// <para>
    /// The handler uses the listener configuration's routing key as its service key, allowing multiple
    /// handlers to be registered for different integration types.
    /// </para>
    /// <para>
    /// Service Bus processor options (PrefetchCount and MaxConcurrentCalls) are configured from the listener
    /// configuration to optimize message throughput and concurrency.
    /// </para>
    /// </remarks>
    internal static IServiceCollection AddAzureServiceBusIntegration<TConfig, TListenerConfig>(this IServiceCollection services,
        TListenerConfig listenerConfiguration)
        where TConfig : class
        where TListenerConfig : IIntegrationListenerConfiguration
    {
        services.TryAddKeyedSingleton<IEventMessageHandler>(serviceKey: listenerConfiguration.RoutingKey, implementationFactory: (provider, _) =>
            new EventIntegrationHandler<TConfig>(
                integrationType: listenerConfiguration.IntegrationType,
                eventIntegrationPublisher: provider.GetRequiredService<IEventIntegrationPublisher>(),
                integrationFilterService: provider.GetRequiredService<IIntegrationFilterService>(),
                cache: provider.GetRequiredKeyedService<IFusionCache>(EventIntegrationsCacheConstants.CacheName),
                configurationRepository: provider.GetRequiredService<IOrganizationIntegrationConfigurationRepository>(),
                groupRepository: provider.GetRequiredService<IGroupRepository>(),
                organizationRepository: provider.GetRequiredService<IOrganizationRepository>(),
                organizationUserRepository: provider.GetRequiredService<IOrganizationUserRepository>(), logger: provider.GetRequiredService<ILogger<EventIntegrationHandler<TConfig>>>())
        );
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
            AzureServiceBusEventListenerService<TListenerConfig>>(provider =>
                new AzureServiceBusEventListenerService<TListenerConfig>(
                    configuration: listenerConfiguration,
                    handler: provider.GetRequiredKeyedService<IEventMessageHandler>(serviceKey: listenerConfiguration.RoutingKey),
                    serviceBusService: provider.GetRequiredService<IAzureServiceBusService>(),
                    serviceBusOptions: new ServiceBusProcessorOptions()
                    {
                        PrefetchCount = listenerConfiguration.EventPrefetchCount,
                        MaxConcurrentCalls = listenerConfiguration.EventMaxConcurrentCalls
                    },
                    loggerFactory: provider.GetRequiredService<ILoggerFactory>()
                )
            )
        );
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
            AzureServiceBusIntegrationListenerService<TListenerConfig>>(provider =>
                new AzureServiceBusIntegrationListenerService<TListenerConfig>(
                    configuration: listenerConfiguration,
                    handler: provider.GetRequiredService<IIntegrationHandler<TConfig>>(),
                    serviceBusService: provider.GetRequiredService<IAzureServiceBusService>(),
                    serviceBusOptions: new ServiceBusProcessorOptions()
                    {
                        PrefetchCount = listenerConfiguration.IntegrationPrefetchCount,
                        MaxConcurrentCalls = listenerConfiguration.IntegrationMaxConcurrentCalls
                    },
                    loggerFactory: provider.GetRequiredService<ILoggerFactory>()
                )
            )
        );

        return services;
    }

    /// <summary>
    /// Registers RabbitMQ-based event integration listeners for a specific integration type.
    /// </summary>
    /// <typeparam name="TConfig">The integration configuration details type (e.g., SlackIntegrationConfigurationDetails).</typeparam>
    /// <typeparam name="TListenerConfig">The listener configuration type implementing IIntegrationListenerConfiguration.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="listenerConfiguration">The listener configuration containing routing keys and message processing settings.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers three key components:
    /// 1. EventIntegrationHandler - Keyed singleton for processing integration events
    /// 2. RabbitMqEventListenerService - Hosted service for listening to event messages from RabbitMQ for
    ///    this integration type
    /// 3. RabbitMqIntegrationListenerService - Hosted service for listening to integration messages from RabbitMQ for
    ///    this integration type
    /// </para>
    ///
    /// <para>
    /// The handler uses the listener configuration's routing key as its service key, allowing multiple
    /// handlers to be registered for different integration types.
    /// </para>
    /// </remarks>
    internal static IServiceCollection AddRabbitMqIntegration<TConfig, TListenerConfig>(this IServiceCollection services,
        TListenerConfig listenerConfiguration)
        where TConfig : class
        where TListenerConfig : IIntegrationListenerConfiguration
    {
        services.TryAddKeyedSingleton<IEventMessageHandler>(serviceKey: listenerConfiguration.RoutingKey, implementationFactory: (provider, _) =>
            new EventIntegrationHandler<TConfig>(
                integrationType: listenerConfiguration.IntegrationType,
                eventIntegrationPublisher: provider.GetRequiredService<IEventIntegrationPublisher>(),
                integrationFilterService: provider.GetRequiredService<IIntegrationFilterService>(),
                cache: provider.GetRequiredKeyedService<IFusionCache>(EventIntegrationsCacheConstants.CacheName),
                configurationRepository: provider.GetRequiredService<IOrganizationIntegrationConfigurationRepository>(),
                groupRepository: provider.GetRequiredService<IGroupRepository>(),
                organizationRepository: provider.GetRequiredService<IOrganizationRepository>(),
                organizationUserRepository: provider.GetRequiredService<IOrganizationUserRepository>(), logger: provider.GetRequiredService<ILogger<EventIntegrationHandler<TConfig>>>())
        );
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
            RabbitMqEventListenerService<TListenerConfig>>(provider =>
                new RabbitMqEventListenerService<TListenerConfig>(
                    handler: provider.GetRequiredKeyedService<IEventMessageHandler>(serviceKey: listenerConfiguration.RoutingKey),
                    configuration: listenerConfiguration,
                    rabbitMqService: provider.GetRequiredService<IRabbitMqService>(),
                    loggerFactory: provider.GetRequiredService<ILoggerFactory>()
                )
            )
        );
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
            RabbitMqIntegrationListenerService<TListenerConfig>>(provider =>
                new RabbitMqIntegrationListenerService<TListenerConfig>(
                    handler: provider.GetRequiredService<IIntegrationHandler<TConfig>>(),
                    configuration: listenerConfiguration,
                    rabbitMqService: provider.GetRequiredService<IRabbitMqService>(),
                    loggerFactory: provider.GetRequiredService<ILoggerFactory>(),
                    timeProvider: provider.GetRequiredService<TimeProvider>()
                )
            )
        );

        return services;
    }

    internal static IServiceCollection AddOrganizationIntegrationCommandsQueries(this IServiceCollection services)
    {
        services.TryAddScoped<ICreateOrganizationIntegrationCommand, CreateOrganizationIntegrationCommand>();
        services.TryAddScoped<IUpdateOrganizationIntegrationCommand, UpdateOrganizationIntegrationCommand>();
        services.TryAddScoped<IDeleteOrganizationIntegrationCommand, DeleteOrganizationIntegrationCommand>();
        services.TryAddScoped<IGetOrganizationIntegrationsQuery, GetOrganizationIntegrationsQuery>();

        return services;
    }

    internal static IServiceCollection AddOrganizationIntegrationConfigurationCommandsQueries(this IServiceCollection services)
    {
        services.TryAddScoped<ICreateOrganizationIntegrationConfigurationCommand, CreateOrganizationIntegrationConfigurationCommand>();
        services.TryAddScoped<IUpdateOrganizationIntegrationConfigurationCommand, UpdateOrganizationIntegrationConfigurationCommand>();
        services.TryAddScoped<IDeleteOrganizationIntegrationConfigurationCommand, DeleteOrganizationIntegrationConfigurationCommand>();
        services.TryAddScoped<IGetOrganizationIntegrationConfigurationsQuery, GetOrganizationIntegrationConfigurationsQuery>();

        return services;
    }

    /// <summary>
    /// Determines if RabbitMQ is enabled for event integrations based on configuration settings.
    /// </summary>
    /// <param name="settings">The global settings containing RabbitMQ configuration.</param>
    /// <returns>True if all required RabbitMQ settings are present; otherwise, false.</returns>
    /// <remarks>
    /// Requires all the following settings to be configured:
    /// - EventLogging.RabbitMq.HostName
    /// - EventLogging.RabbitMq.Username
    /// - EventLogging.RabbitMq.Password
    /// - EventLogging.RabbitMq.EventExchangeName
    /// </remarks>
    internal static bool IsRabbitMqEnabled(GlobalSettings settings)
    {
        return CoreHelpers.SettingHasValue(settings.EventLogging.RabbitMq.HostName) &&
               CoreHelpers.SettingHasValue(settings.EventLogging.RabbitMq.Username) &&
               CoreHelpers.SettingHasValue(settings.EventLogging.RabbitMq.Password) &&
               CoreHelpers.SettingHasValue(settings.EventLogging.RabbitMq.EventExchangeName);
    }

    /// <summary>
    /// Determines if Azure Service Bus is enabled for event integrations based on configuration settings.
    /// </summary>
    /// <param name="settings">The global settings containing Azure Service Bus configuration.</param>
    /// <returns>True if all required Azure Service Bus settings are present; otherwise, false.</returns>
    /// <remarks>
    /// Requires both of the following settings to be configured:
    /// - EventLogging.AzureServiceBus.ConnectionString
    /// - EventLogging.AzureServiceBus.EventTopicName
    /// </remarks>
    internal static bool IsAzureServiceBusEnabled(GlobalSettings settings)
    {
        return CoreHelpers.SettingHasValue(settings.EventLogging.AzureServiceBus.ConnectionString) &&
               CoreHelpers.SettingHasValue(settings.EventLogging.AzureServiceBus.EventTopicName);
    }
}
