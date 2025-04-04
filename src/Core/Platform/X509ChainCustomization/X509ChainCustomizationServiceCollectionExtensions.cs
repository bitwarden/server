using Bit.Core.Platform.X509ChainCustomization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up the ability to provide customization to how TLS works in an <see cref="IServiceCollection"/>.
/// </summary>
public static class X509ChainCustomizationServiceCollectionExtensions
{
    /// <summary>
    /// Configures X509ChainPolicy customization through the root level <c>TlsOptions</c> configuration section
    /// and configures the primary <see cref="HttpMessageHandler"/> to use custom certificate validation
    /// when customized to do so.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for additional chaining.</returns>
    public static IServiceCollection AddX509ChainCustomization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<X509ChainOptions>()
            .BindConfiguration(nameof(X509ChainOptions));

        // Use TryAddEnumerable to make sure `PostConfigureTlsOptions` isn't added multiple
        // times even if this method is called multiple times.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<X509ChainOptions>, PostConfigureX509ChainOptions>());

        services.AddHttpClient()
            .ConfigureHttpClientDefaults(builder =>
            {
                builder.ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var tlsOptions = sp.GetRequiredService<IOptions<X509ChainOptions>>().Value;

                    var handler = new HttpClientHandler();

                    if (tlsOptions.TryGetCustomRemoteCertificateValidationCallback(out var callback))
                    {
                        handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, errors) =>
                        {
                            return callback(certificate, chain, errors);
                        };
                    }

                    return handler;
                });
            });

        return services;
    }
}
