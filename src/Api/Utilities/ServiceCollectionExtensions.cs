using System.Collections.Generic;
using System.IO;
using Bit.Core;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace Bit.Api.Utilities
{
    public static class ServiceCollectionExtensions
    {
        public static void AddSwagger(this IServiceCollection services, GlobalSettings globalSettings)
        {
            services.AddSwaggerGen(config =>
            {
                config.SwaggerDoc("public", new Info
                {
                    Title = "Bitwarden Public API",
                    Version = "latest",
                    Contact = new Contact
                    {
                        Name = "Bitwarden Support",
                        Url = "https://bitwarden.com",
                        Email = "support@bitwarden.com"
                    },
                    Description = "The Bitwarden public APIs.",
                    License = new License
                    {
                        Name = "GNU Affero General Public License v3.0",
                        Url = "https://github.com/bitwarden/server/blob/master/LICENSE.txt"
                    }
                });
                // config.SwaggerDoc("internal", new Info { Title = "Bitwarden Internal API", Version = "latest" });

                config.AddSecurityDefinition("OAuth2 Client Credentials", new OAuth2Scheme
                {
                    Type = "oauth2",
                    Flow = "application",
                    TokenUrl = $"{globalSettings.BaseServiceUri.Identity}/connect/token",
                    Scopes = new Dictionary<string, string>
                    {
                        { "api.organization", "Organization APIs" },
                    },
                });

                config.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>
                {
                    { "OAuth2 Client Credentials", new[] { "api.organization" } }
                });

                config.DescribeAllParametersInCamelCase();
                // config.UseReferencedDefinitionsForEnums();

                var apiFilePath = Path.Combine(System.AppContext.BaseDirectory, "Api.xml");
                config.IncludeXmlComments(apiFilePath, true);
                var coreFilePath = Path.Combine(System.AppContext.BaseDirectory, "Core.xml");
                config.IncludeXmlComments(coreFilePath);
            });
        }
    }
}
