using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Core.Utilities;

public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddInstallationAuthentication(this IHttpClientBuilder builder,
        Action<ConnectTokenOptions> configureOptions)
    {
        builder.Services.Configure(builder.Name, configureOptions);

        return builder.AddHttpMessageHandler(sp =>
        {
            return new InstallationAuthenticatingHandler(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientNames.CloudIdentity),
                sp.GetRequiredService<ILogger<InstallationAuthenticatingHandler>>(),
                sp.GetRequiredService<IOptionsMonitor<ConnectTokenOptions>>(),
                builder.Name
            );
        });
    }

    public static IHttpClientBuilder ConfigureBaseAddress(this IHttpClientBuilder builder,
        Func<GlobalSettings, string> retrieveBaseAddress)
    {
        return builder.ConfigureHttpClient((sp, client) =>
        {
            var gs = sp.GetRequiredService<GlobalSettings>();
            client.BaseAddress = new Uri(retrieveBaseAddress(gs));
        });
    }
}
