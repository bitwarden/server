using System.Globalization;
using Azure.Storage.Queues;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bit.Notifications;

public class Startup
{
    private const string NotificationsSettingsSection = "globalSettings:notifications";

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
        services.AddGlobalSettingsBridge();
        services.AddCors();
        services.AddOptions<NotificationsSettings>()
            .Bind(Configuration.GetSection(NotificationsSettingsSection))
            .Configure(settings =>
                settings.InstallationId = Configuration.GetValue<Guid>("globalSettings:installation:id"));

        // Identity
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>());
        services.AddAuthorization(config =>
        {
            config.AddPolicy("Application", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("amr", "Application", "external");
                policy.RequireClaim("scope", "api");
            });
            config.AddPolicy("Internal", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", "internal");
            });
        });

        // SignalR
        var signalRServerBuilder = services.AddSignalR().AddMessagePackProtocol(options =>
        {
            options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                .WithResolver(MessagePack.Resolvers.CompositeResolver.Create(
                    JsonElementResolver.Instance,
                    MessagePack.Resolvers.ContractlessStandardResolver.Instance));
        });
        // The backplane is chosen while services are registered, before options can be resolved, so bind the
        // settings directly here.
        var redisConnectionString = Configuration.GetSection(NotificationsSettingsSection)
            .Get<NotificationsSettings>()?.RedisConnectionString;
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            signalRServerBuilder.AddStackExchangeRedis(redisConnectionString,
                options =>
                {
                    options.Configuration.ChannelPrefix = "Notifications";
                });
        }
        services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
        services.AddSingleton<HubHelpers>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<HeartbeatHostedService>();
        services.TryAddKeyedSingleton<QueueClient>("notifications", (sp, _) =>
            new QueueClient(
                sp.GetRequiredService<IOptions<NotificationsSettings>>().Value.ConnectionString,
                "notifications"));
        services.AddHostedService<AzureQueueHostedService>();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IBitwardenEnvironment environment)
    {
        // Add general security headers
        app.UseSecurityHeaders();

        // Forwarding Headers
        if (environment.SelfHosted)
        {
            app.UseForwardedHeaders();
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Add routing
        app.UseRouting();

        // Add Cors
        app.UseCors();

        // Add authentication to the request pipeline.
        app.UseBitwardenAuthentication();
        app.UseAuthorization();

        // Add endpoints to the request pipeline.
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<NotificationsHub>("/hub", options =>
            {
                options.ApplicationMaxBufferSize = 2048;
                options.TransportMaxBufferSize = 4096;
            });
            endpoints.MapHub<AnonymousNotificationsHub>("/anonymous-hub", options =>
            {
                options.ApplicationMaxBufferSize = 2048;
                options.TransportMaxBufferSize = 4096;
            });
            // Cloud receives notifications from the Azure queue, so only self-hosted serves /send.
            if (environment.SelfHosted)
            {
                endpoints.MapSendEndpoint();
            }
            endpoints.MapInfoEndpoints();
            endpoints.MapVersionEndpoint();
        });
    }
}
