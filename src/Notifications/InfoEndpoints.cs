namespace Bit.Notifications;

internal static class InfoEndpoints
{
    /// <summary>
    /// Maps <c>GET /alive</c> and <c>GET /now</c>, which return the current UTC time for health checks.
    /// </summary>
    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/alive", GetNow);
        endpoints.MapGet("/now", GetNow);
        return endpoints;
    }

    private static DateTime GetNow(TimeProvider timeProvider) => timeProvider.GetUtcNow().UtcDateTime;
}
