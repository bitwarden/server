﻿using Bit.Api.AdminConsole.Authorization;
using Bit.Api.Tools.Authorization;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;
using Bit.Core.IdentityServer;
using Bit.Core.PhishingDomainFeatures;
using Bit.Core.PhishingDomainFeatures.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Repositories.Implementations;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.SharedWeb.Health;
using Bit.SharedWeb.Swagger;
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

            config.SchemaFilter<EnumSchemaFilter>();

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
        services.AddScoped<IAuthorizationHandler, BulkCollectionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CollectionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, GroupAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, VaultExportAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, SecurityTaskAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, SecurityTaskOrganizationAuthorizationHandler>();

        services.AddScoped<IAuthorizationHandler, OrganizationRequirementHandler>();
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
