using System.Collections.Generic;
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
                config.SwaggerDoc("public", new Info { Title = "Bitwarden Public API", Version = "latest" });
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
            });
        }
    }
}
