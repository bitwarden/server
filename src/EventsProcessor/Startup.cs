using System.Globalization;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.IdentityModel.Logging;
using TableStorageRepos = Bit.Core.Repositories.TableStorage;

namespace Bit.EventsProcessor;

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

        // Hosted Services

        // Optional Azure Service Bus Listeners
        if (CoreHelpers.SettingHasValue(globalSettings.EventLogging.AzureServiceBus.ConnectionString) &&
            CoreHelpers.SettingHasValue(globalSettings.EventLogging.AzureServiceBus.TopicName))
        {
            services.AddSingleton<IEventRepository, TableStorageRepos.EventRepository>();
            services.AddSingleton<AzureTableStorageEventHandler>();
            services.AddKeyedSingleton<IEventWriteService, RepositoryEventWriteService>("persistent");
            services.AddSingleton<IHostedService>(provider =>
                new AzureServiceBusEventListenerService(
                    provider.GetRequiredService<AzureTableStorageEventHandler>(),
                    provider.GetRequiredService<ILogger<AzureServiceBusEventListenerService>>(),
                    globalSettings,
                    globalSettings.EventLogging.AzureServiceBus.EventRepositorySubscriptionName));

            if (CoreHelpers.SettingHasValue(globalSettings.EventLogging.WebhookUrl))
            {
                services.AddSingleton<WebhookEventHandler>();
                services.AddHttpClient(WebhookEventHandler.HttpClientName);

                services.AddSingleton<IHostedService>(provider =>
                    new AzureServiceBusEventListenerService(
                        provider.GetRequiredService<WebhookEventHandler>(),
                        provider.GetRequiredService<ILogger<AzureServiceBusEventListenerService>>(),
                        globalSettings,
                        globalSettings.EventLogging.AzureServiceBus.WebhookSubscriptionName));
            }
        }
        services.AddHostedService<AzureQueueHostedService>();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings)
    {
        IdentityModelEventSource.ShowPII = true;
        app.UseSerilog(env, appLifetime, globalSettings);
        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/alive",
                async context => await context.Response.WriteAsJsonAsync(System.DateTime.UtcNow));
            endpoints.MapGet("/now",
                async context => await context.Response.WriteAsJsonAsync(System.DateTime.UtcNow));
            endpoints.MapGet("/version",
                async context => await context.Response.WriteAsJsonAsync(AssemblyHelpers.GetVersion()));

        });
    }
}
