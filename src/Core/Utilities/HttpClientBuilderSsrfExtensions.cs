#nullable enable

using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Utilities;

/// <summary>
/// Extension methods for registering SSRF protection on named HTTP clients.
/// </summary>
public static class HttpClientBuilderSsrfExtensions
{
    /// <summary>
    /// Adds SSRF protection to an HTTP client by registering the <see cref="SsrfProtectionHandler"/>
    /// as an additional delegating handler and disabling automatic redirects on the primary handler.
    /// This ensures that all requests made by the client resolve DNS before connecting and block
    /// requests to internal/private IP ranges, including those reached via HTTP redirects.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> so that additional calls can be chained.</returns>
    public static IHttpClientBuilder AddSsrfProtection(this IHttpClientBuilder builder)
    {
        builder.Services.AddTransient<SsrfProtectionHandler>();
        builder.AddHttpMessageHandler<SsrfProtectionHandler>();

        // Disable auto-redirect on the primary handler so that redirects pass back through
        // SsrfProtectionHandler for validation on each hop. Without this, the inner handler
        // follows redirects internally, bypassing SSRF checks on redirect targets.
        builder.ConfigurePrimaryHttpMessageHandler((handler, _) =>
        {
            switch (handler)
            {
                case HttpClientHandler httpClientHandler:
                    httpClientHandler.AllowAutoRedirect = false;
                    break;
                case SocketsHttpHandler socketsHttpHandler:
                    socketsHttpHandler.AllowAutoRedirect = false;
                    break;
            }
        });

        return builder;
    }
}
