#if ENABLE_NGROK_COMMUNITY_PLUGIN
namespace Bit.AppHost.Resources;

/// <summary>
/// Adds an ngrok tunnel that exposes a local service, such as the billing webhook endpoint, to the internet.
/// Compiled only when the ngrok community plugin is enabled in the project file.
/// </summary>
public static class NgrokResource
{
    extension(IDistributedApplicationBuilder builder)
    {
        public void ConfigureNgrok((IResourceBuilder<ProjectResource>, string) tunnelResource)
        {
            var rawToken = builder.Configuration["NgrokAuthToken"];
            if (string.IsNullOrWhiteSpace(rawToken))
                return;

            var authToken = builder.AddParameter("ngrok-auth-token", rawToken, secret: true);
            builder.AddNgrok("billing-webhook-ngrok-endpoint", endpointPort: 59600)
                .WithAuthToken(authToken)
                .WithTunnelEndpoint(tunnelResource.Item1, tunnelResource.Item2)
                .WithExplicitStart();
        }
    }
}
#endif
