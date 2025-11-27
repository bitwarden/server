using Bit.Api.AdminConsole.Authorization;
using Bit.Api.Tools.Authorization;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.PhishingDomainFeatures;
using Bit.Core.PhishingDomainFeatures.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Repositories.Implementations;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.SharedWeb.Health;
using Bit.SharedWeb.Swagger;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;

namespace Bit.Api.Utilities;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the generation of swagger.json OpenAPI spec.
    /// </summary>
    public static void AddSwaggerGen(this IServiceCollection services, GlobalSettings globalSettings, IWebHostEnvironment environment)
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
                Description = """
                              This schema documents the endpoints available to the Public API, which provides
                              organizations tools for managing members, collections, groups, event logs, and policies.
                              If you are looking for the Vault Management API, refer instead to
                              [this document](https://bitwarden.com/help/vault-management-api/).

                              **Note:** your authorization must match the server you have selected.
                              """,
                License = new OpenApiLicense
                {
                    Name = "GNU Affero General Public License v3.0",
                    Url = new Uri("https://github.com/bitwarden/server/blob/master/LICENSE.txt")
                }
            });

            config.SwaggerDoc("internal", new OpenApiInfo { Title = "Bitwarden Internal API", Version = "latest" });

            config.AddSwaggerServerWithSecurity(
                serverId: "US_server",
                serverUrl: "https://api.bitwarden.com",
                identityTokenUrl: "https://identity.bitwarden.com/connect/token",
                serverDescription: "US server");

            config.AddSwaggerServerWithSecurity(
                serverId: "EU_server",
                serverUrl: "https://api.bitwarden.eu",
                identityTokenUrl: "https://identity.bitwarden.eu/connect/token",
                serverDescription: "EU server");

            config.AddSwaggerServerWithSecurity(
                serverId: "Local_server",
                serverUrl: globalSettings.BaseServiceUri.Api,
                identityTokenUrl: $"{globalSettings.BaseServiceUri.Identity}/connect/token",
                serverDescription: "Self-hosted or local server");

            config.DescribeAllParametersInCamelCase();
            // config.UseReferencedDefinitionsForEnums();

            config.InitializeSwaggerFilters(environment);

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
        });
    }

    public static void AddAuthorizationHandlers(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, VaultExportAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, SecurityTaskAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, SecurityTaskOrganizationAuthorizationHandler>();

        // Admin Console authorization handlers
        services.AddAdminConsoleAuthorizationHandlers();
    }

    public static void AddPhishingDomainServices(this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddHttpClient("PhishingDomains", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", globalSettings.SelfHosted ? "Bitwarden Self-Hosted" : "Bitwarden");
            client.Timeout = TimeSpan.FromSeconds(1000); // the source list is very slow
        });

        services.AddSingleton<AzurePhishingDomainStorageService>();
        services.AddSingleton<IPhishingDomainRepository, AzurePhishingDomainRepository>();

        if (globalSettings.SelfHosted)
        {
            services.AddScoped<ICloudPhishingDomainQuery, CloudPhishingDomainRelayQuery>();
        }
        else
        {
            services.AddScoped<ICloudPhishingDomainQuery, CloudPhishingDomainDirectQuery>();
        }
    }
}
