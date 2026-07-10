using System.Reflection;
using System.Text.Json.Serialization;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.Tools.Authorization;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.SharedWeb.Health;
using Bit.SharedWeb.Swagger;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

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

            // Configure Bitwarden cloud servers. These will appear in the swagger.json build artifact
            // used for our help center. These are overwritten with the local server when running in self-hosted
            // or dev mode (see Api Startup.cs).
            foreach (var regionConfig in CloudRegionConfig.All)
            {
                config.AddSwaggerServerWithSecurity(
                    serverId: $"{regionConfig.Region}_server",
                    serverUrl: regionConfig.ApiUrl,
                    identityTokenUrl: $"{regionConfig.IdentityUrl}/connect/token",
                    serverDescription: $"{regionConfig.Region} server");
            }

            // Security scheme for send access token endpoints (V2). The x-explicit-bearer-token
            // extension signals the SDK code generator to emit an explicit Bearer token parameter
            // instead of injecting the user session token via middleware.
            config.AddSecurityDefinition("send-access-bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Send access token obtained from /connect/token using the send_access grant.",
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    { "x-explicit-bearer-token", new JsonNodeExtension(true) }
                }
            });

            config.DescribeAllParametersInCamelCase();

            // Emit oneOf/discriminator for polymorphic type hierarchies that explicitly opt in via
            // [JsonPolymorphic] + [JsonDerivedType] attributes (currently only AccessConditionModel).
            // We use explicit selectors instead of the default assembly-scanning SubTypesSelector
            // because the default scanner picks up the entire ResponseModel/ErrorResponseModel
            // inheritance tree, which pulls two same-named ErrorResponseModel classes into the same
            // document and causes a schemaId collision. Explicit selectors scope polymorphism strictly
            // to [JsonPolymorphic] opt-in hierarchies, leaving everything else untouched.
            config.UseOneOfForPolymorphism();
            config.SelectSubTypesUsing(baseType =>
                baseType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                    .Select(a => a.DerivedType));
            config.SelectDiscriminatorNameUsing(baseType =>
            {
                if (!baseType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false).Any())
                {
                    return null;
                }

                // System.Text.Json's discriminator when [JsonPolymorphic] is absent or sets no
                // name is "$type"; mirror that default so an opted-in hierarchy never emits oneOf
                // schemas without a discriminator (Swashbuckle skips the discriminator entirely
                // when this selector returns null, and clients generated from such a spec omit
                // the property the server requires).
                return baseType.GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false)
                    ?.TypeDiscriminatorPropertyName ?? "$type";
            });
            config.SelectDiscriminatorValueUsing(subType =>
            {
                // [JsonDerivedType] is declared on the BASE type; walk up until we find it.
                var t = subType.BaseType;
                while (t != null)
                {
                    var attr = t.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                        .FirstOrDefault(a => a.DerivedType == subType);
                    if (attr != null)
                    {
                        return attr.TypeDiscriminator?.ToString();
                    }
                    t = t.BaseType;
                }
                return null;
            });

            // config.UseReferencedDefinitionsForEnums();

            config.InitializeSwaggerFilters(environment);

            // Include every assembly documentation file emitted into the output directory (each XML is paired with
            // its .dll). A project's docs surface in the spec simply by emitting a <DocumentationFile> — no change is
            // needed here, and commercial-only assemblies (e.g. Pam) are picked up when present.
            // includeControllerXmlComments is on so controller and Minimal API summaries are read too.
            foreach (var xmlDocPath in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.xml")
                         .Where(xmlDocPath => File.Exists(Path.ChangeExtension(xmlDocPath, ".dll"))))
            {
                config.IncludeXmlComments(xmlDocPath, includeControllerXmlComments: true);
            }
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
        // SecurityTaskAuthorizationHandler must remain scoped. It caches cipher permissions per-request.
        // Changing to singleton would allow one user's cached permissions to be reused by other users in the same organization.
        services.AddScoped<IAuthorizationHandler, SecurityTaskAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, SecurityTaskOrganizationAuthorizationHandler>();

        // Admin Console authorization handlers
        services.AddAdminConsoleAuthorizationHandlers();
    }
}
