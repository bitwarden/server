using Bit.Commercial.Pam.Api.Endpoints.Handlers;

namespace Bit.Commercial.Pam.Api;

public static class PamApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PAM Minimal API endpoint handlers. The endpoints (see <c>PamEndpointsExtensions</c>) resolve
    /// these from DI. PAM is a commercial feature, so this is only wired in non-OSS builds.
    /// </summary>
    public static IServiceCollection AddPamApiServices(this IServiceCollection services)
    {
        services.AddScoped<LeaseEndpointsHandler>();
        services.AddScoped<AccessRequestEndpointsHandler>();
        services.AddScoped<AccessRuleEndpointsHandler>();
        services.AddScoped<CipherLeaseEndpointsHandler>();
        return services;
    }
}
