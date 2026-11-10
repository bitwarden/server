#if ENABLE_NGROK_COMMUNITY_PLUGIN
using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds an ngrok tunnel that exposes a local service, such as the billing webhook endpoint, to the internet.
/// Compiled only when the ngrok community plugin is enabled in the project file.
/// </summary>
internal static class NgrokResource
{
    private const int _endpointPort = 59600;

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds an ngrok tunnel to the given resource endpoint when <c>NgrokAuthToken</c> is set; does nothing
        /// otherwise.
        /// </summary>
        internal void ConfigureNgrok((IResourceBuilder<ProjectResource>, string) tunnelResource)
        {
            var rawToken = builder.Settings.NgrokAuthToken;
            if (string.IsNullOrWhiteSpace(rawToken))
                return;

            var authToken = builder.AddParameter("ngrok-auth-token", rawToken, secret: true);
            builder.AddNgrok("billing-webhook-ngrok-endpoint", endpointPort: _endpointPort)
                .WithAuthToken(authToken)
                .WithTunnelEndpoint(tunnelResource.Item1, tunnelResource.Item2)
                .WithExplicitStart();
        }
    }
}
#endif
