using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Core.Utilities;

public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddInstallationAuthentication(this IHttpClientBuilder builder,
        string identityClientName,
        string optionsName,
        Action<ConnectTokenOptions> configureOptions)
    {
        builder.Services.Configure(optionsName, configureOptions);

        return builder.AddHttpMessageHandler(sp =>
        {
            return new InstallationAuthenticatingHandler(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(identityClientName),
                sp.GetRequiredService<ILogger<InstallationAuthenticatingHandler>>(),
                sp.GetRequiredService<IOptionsMonitor<ConnectTokenOptions>>(),
                optionsName
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
