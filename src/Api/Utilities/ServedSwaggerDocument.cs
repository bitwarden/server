using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Bit.SharedWeb.Swagger;
using Microsoft.OpenApi;

namespace Bit.Api.Utilities;

internal static class ServedSwaggerDocument
{
    /// <summary>
    /// Points a served document at this installation's server and its local client credentials scheme.
    /// </summary>
    internal static void Configure(OpenApiDocument swaggerDoc, GlobalSettings globalSettings)
    {
        swaggerDoc.Servers =
        [
            new()
            {
                Url = globalSettings.BaseServiceUri.Api,
            }
        ];

        OrganizationScopesOperationFilter.RebindOperationRequirements(swaggerDoc, "oauth2-client-credentials");

        swaggerDoc.Components ??= new OpenApiComponents();
        swaggerDoc.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            {
                "oauth2-client-credentials",
                new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri($"{globalSettings.BaseServiceUri.Identity}/connect/token"),
                            Scopes = OrganizationScopesOperationFilter.GetClientCredentialsScopes("Organization APIs")
                        }
                    }
                }
            },
            {
                "send-access-bearer",
                new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Send access token obtained from /connect/token using the send_access grant.",
                    Extensions = new Dictionary<string, IOpenApiExtension>
                    {
                        { "x-explicit-bearer-token", new JsonNodeExtension(true) }
                    }
                }
            }
        };

        swaggerDoc.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("oauth2-client-credentials", swaggerDoc)] = [ApiScopes.ApiOrganization]
            },
        ];

        swaggerDoc.Workspace = new OpenApiWorkspace();
        swaggerDoc.RegisterComponents();
    }
}
