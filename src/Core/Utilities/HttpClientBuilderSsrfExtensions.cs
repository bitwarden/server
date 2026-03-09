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
    /// as an additional delegating handler. This ensures that all requests made by the client
    /// resolve DNS before connecting and block requests to internal/private IP ranges.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> so that additional calls can be chained.</returns>
    public static IHttpClientBuilder AddSsrfProtection(this IHttpClientBuilder builder)
    {
        builder.Services.AddTransient<SsrfProtectionHandler>();
        builder.AddHttpMessageHandler<SsrfProtectionHandler>();
        return builder;
    }
}
