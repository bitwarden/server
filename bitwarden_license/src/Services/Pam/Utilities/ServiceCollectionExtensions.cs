using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;

namespace Bit.Services.Pam.Utilities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPamServices(this IServiceCollection services)
    {
        // The SDK's serde tagging accepts the 'kind' discriminator at any position in the object;
        // without this flag System.Text.Json requires it first and fails binding with an
        // exception that surfaces as a 500 (see AccessConditionModel).
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.AllowOutOfOrderMetadataProperties = true);

        // Minimal API endpoint handlers. The endpoints (see PamEndpointsExtensions) resolve these from DI.
        services.AddScoped<AccessRequestEndpointsHandler>();
        services.AddScoped<AccessRuleEndpointsHandler>();

        services.AddPamOpenApiEndpointDataSource();

        return services;
    }

    /// <summary>
    /// Registers the PAM Minimal API endpoints (see <c>MapPamEndpoints</c>) so the offline OpenAPI generator
    /// (<c>dotnet swagger tofile</c>) can discover them — it never runs the <c>Configure</c> pipeline where the
    /// endpoints are normally mapped. The discovery and swagger-only gating live in
    /// <see cref="EndpointDataSourceServiceCollectionExtensions.AddOpenApiEndpointDataSource"/>.
    /// </summary>
    private static IServiceCollection AddPamOpenApiEndpointDataSource(this IServiceCollection services)
        => services.AddOpenApiEndpointDataSource(endpoints => endpoints.MapPamEndpoints());
}
