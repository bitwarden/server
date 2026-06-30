using Bit.Commercial.Pam.Api.Endpoints;
using Bit.Commercial.Pam.Api.Endpoints.Handlers;
using Bit.HttpExtensions;

namespace Bit.Commercial.Pam.Utilities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommercialPamServices(this IServiceCollection services)
    {
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
