﻿using System.Globalization;
using Bit.Core;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Core.AdminConsole.Services.NoopImplementations;
using Bit.Core.Context;
using Bit.Core.IdentityServer;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using IdentityModel;

namespace Bit.Events;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
        Environment = env;
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment Environment { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Repositories
        services.AddDatabaseRepositories(globalSettings);

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();

        // Identity
        services.AddIdentityAuthenticationServices(globalSettings, Environment, config =>
        {
            config.AddPolicy("Application", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.AuthenticationMethod, "Application", "external");
                policy.RequireClaim(JwtClaimTypes.Scope, ApiScopes.Api);
            });
        });

        // Services
        var usingServiceBusAppCache = CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ConnectionString) &&
            CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ApplicationCacheTopicName);
        if (usingServiceBusAppCache)
        {
            services.AddSingleton<IApplicationCacheService, InMemoryServiceBusApplicationCacheService>();
        }
        else
        {
            services.AddSingleton<IApplicationCacheService, InMemoryApplicationCacheService>();
        }

        if (!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Events.ConnectionString))
        {
            services.AddKeyedSingleton<IEventWriteService, AzureQueueEventWriteService>("storage");

            if (CoreHelpers.SettingHasValue(globalSettings.EventLogging.AzureServiceBus.ConnectionString) &&
                CoreHelpers.SettingHasValue(globalSettings.EventLogging.AzureServiceBus.TopicName))
            {
                services.AddKeyedSingleton<IEventWriteService, AzureServiceBusEventWriteService>("broadcast");
            }
            else
            {
                services.AddKeyedSingleton<IEventWriteService, NoopEventWriteService>("broadcast");
            }
        }
        else
        {
            services.AddKeyedSingleton<IEventWriteService, RepositoryEventWriteService>("storage");

            if (CoreHelpers.SettingHasValue(globalSettings.EventLogging.RabbitMq.HostName) &&
                CoreHelpers.SettingHasValue(globalSettings.EventLogging.RabbitMq.Username) &&
                CoreHelpers.SettingHasValue(globalSettings.EventLogging.RabbitMq.Password) &&
                CoreHelpers.SettingHasValue(globalSettings.EventLogging.RabbitMq.ExchangeName))
            {
                services.AddKeyedSingleton<IEventWriteService, RabbitMqEventWriteService>("broadcast");
            }
            else
            {
                services.AddKeyedSingleton<IEventWriteService, NoopEventWriteService>("broadcast");
            }
        }
        services.AddScoped<IEventWriteService>(sp =>
        {
            var featureService = sp.GetRequiredService<IFeatureService>();
            var key = featureService.IsEnabled(FeatureFlagKeys.EventBasedOrganizationIntegrations)
                ? "broadcast" : "storage";
            return sp.GetRequiredKeyedService<IEventWriteService>(key);
        });
        services.AddScoped<IEventService, EventService>();

        services.AddOptionality();

        // Mvc
        services.AddMvc(config =>
        {
            config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
        });

        if (usingServiceBusAppCache)
        {
            services.AddHostedService<Core.HostedServices.ApplicationCacheHostedService>();
        }

        // Optional RabbitMQ Listeners
        if (CoreHelpers.SettingHasValue(globalSettings.EventLogging.RabbitMq.HostName) &&
            CoreHelpers.SettingHasValue(globalSettings.EventLogging.RabbitMq.Username) &&
            CoreHelpers.SettingHasValue(globalSettings.EventLogging.RabbitMq.Password) &&
            CoreHelpers.SettingHasValue(globalSettings.EventLogging.RabbitMq.ExchangeName))
        {
            services.AddSingleton<EventRepositoryHandler>();
            services.AddKeyedSingleton<IEventWriteService, RepositoryEventWriteService>("persistent");
            services.AddSingleton<IHostedService>(provider =>
                new RabbitMqEventListenerService(
                    provider.GetRequiredService<EventRepositoryHandler>(),
                    provider.GetRequiredService<ILogger<RabbitMqEventListenerService>>(),
                    globalSettings,
                    globalSettings.EventLogging.RabbitMq.EventRepositoryQueueName));

            if (CoreHelpers.SettingHasValue(globalSettings.Slack.ClientId) &&
                CoreHelpers.SettingHasValue(globalSettings.Slack.ClientSecret) &&
                CoreHelpers.SettingHasValue(globalSettings.Slack.Scopes))
            {
                services.AddHttpClient(SlackService.HttpClientName);
                services.AddSingleton<ISlackService, SlackService>();
            }
            else
            {
                services.AddSingleton<ISlackService, NoopSlackService>();
            }
            services.AddSingleton<SlackEventHandler>();
            services.AddSingleton<IHostedService>(provider =>
                new RabbitMqEventListenerService(
                    provider.GetRequiredService<SlackEventHandler>(),
                    provider.GetRequiredService<ILogger<RabbitMqEventListenerService>>(),
                    globalSettings,
                    globalSettings.EventLogging.RabbitMq.SlackQueueName));

            services.AddHttpClient(WebhookEventHandler.HttpClientName);
            services.AddSingleton<WebhookEventHandler>();
            services.AddSingleton<IHostedService>(provider =>
                new RabbitMqEventListenerService(
                    provider.GetRequiredService<WebhookEventHandler>(),
                    provider.GetRequiredService<ILogger<RabbitMqEventListenerService>>(),
                    globalSettings,
                    globalSettings.EventLogging.RabbitMq.WebhookQueueName));
        }
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings)
    {
        app.UseSerilog(env, appLifetime, globalSettings);

        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // Forwarding Headers
        if (globalSettings.SelfHosted)
        {
            app.UseForwardedHeaders(globalSettings);
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Default Middleware
        app.UseDefaultMiddleware(env, globalSettings);

        // Add routing
        app.UseRouting();

        // Add Cors
        app.UseCors(policy => policy.SetIsOriginAllowed(o => CoreHelpers.IsCorsOriginAllowed(o, globalSettings))
            .AllowAnyMethod().AllowAnyHeader().AllowCredentials());

        // Add authentication and authorization to the request pipeline.
        app.UseAuthentication();
        app.UseAuthorization();

        // Add current context
        app.UseMiddleware<CurrentContextMiddleware>();

        // Add MVC to the request pipeline.
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
