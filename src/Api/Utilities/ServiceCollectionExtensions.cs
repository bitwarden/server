using Bit.Core.Settings;
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
                            { "api.organization", "Organization APIs" },
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
                    new[] { "api.organization" }
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
}
