using Bit.Api.Vault.AuthorizationHandlers;
using Bit.Core.IdentityServer;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;

namespace Bit.Api.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddSwagger(this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddSwaggerGen(config =>
        {
            config.SwaggerDoc("public", new OpenApiInfo
            {
                Title = "Bitwarden Public API",
                Version = "latest",
                Contact = new OpenApiContact
                {
                    Name = "Bitwarden Support",
                    Url = new Uri("https://bitwarden.com"),
                    Email = "support@bitwarden.com"
                },
                Description = "The Bitwarden public APIs.",
                License = new OpenApiLicense
                {
                    Name = "GNU Affero General Public License v3.0",
                    Url = new Uri("https://github.com/bitwarden/server/blob/master/LICENSE.txt")
                }
            });
            config.SwaggerDoc("internal", new OpenApiInfo { Title = "Bitwarden Internal API", Version = "latest" });

            config.AddSecurityDefinition("oauth2-client-credentials", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    ClientCredentials = new OpenApiOAuthFlow
                    {
                        TokenUrl = new Uri($"{globalSettings.BaseServiceUri.Identity}/connect/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            { ApiScopes.ApiOrganization, "Organization APIs" },
                        },
                    }
                },
            });

            config.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "oauth2-client-credentials"
                        },
                    },
                    new[] { ApiScopes.ApiOrganization }
                }
            });

            config.DescribeAllParametersInCamelCase();
            // config.UseReferencedDefinitionsForEnums();

            var apiFilePath = Path.Combine(AppContext.BaseDirectory, "Api.xml");
            config.IncludeXmlComments(apiFilePath, true);
            var coreFilePath = Path.Combine(AppContext.BaseDirectory, "Core.xml");
            config.IncludeXmlComments(coreFilePath);
        });
    }

    public static void AddHealthChecks(this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddHealthCheckServices(globalSettings, builder =>
        {
            var identityUri = new Uri(globalSettings.BaseServiceUri.Identity
                                      + "/.well-known/openid-configuration");

            builder.AddUrlGroup(identityUri, "identity");

            if (CoreHelpers.SettingHasValue(globalSettings.SqlServer.ConnectionString))
            {
                builder.AddSqlServer(globalSettings.SqlServer.ConnectionString);
            }

            if (CoreHelpers.SettingHasValue(globalSettings.Redis.ConnectionString))
            {
                builder.AddRedis(globalSettings.Redis.ConnectionString);
            }

            if (CoreHelpers.SettingHasValue(globalSettings.Storage.ConnectionString))
            {
                builder.AddAzureQueueStorage(globalSettings.Storage.ConnectionString, name: "storage_queue")
                    .AddAzureQueueStorage(globalSettings.Events.ConnectionString, name: "events_queue");
            }

            if (CoreHelpers.SettingHasValue(globalSettings.Notifications.ConnectionString))
            {
                builder.AddAzureQueueStorage(globalSettings.Notifications.ConnectionString,
                    name: "notifications_queue");
            }

            if (CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ConnectionString))
            {
                builder.AddAzureServiceBusTopic(_ => globalSettings.ServiceBus.ConnectionString,
                    _ => globalSettings.ServiceBus.ApplicationCacheTopicName, name: "service_bus");
            }

            if (CoreHelpers.SettingHasValue(globalSettings.Mail.SendGridApiKey))
            {
                builder.AddSendGrid(globalSettings.Mail.SendGridApiKey);
            }
        });
    }

    public static void AddAuthorizationHandlers(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, CollectionAuthorizationHandler>();
    }
}
