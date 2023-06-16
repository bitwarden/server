using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Icons.Test.Services;

public class ServiceTestBase
{
    private ServiceProvider _provider;

    public ServiceTestBase()
    {
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddDebug();
        });

        services.AddHttpClient("Icons", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.MaxResponseContentBufferSize = 5000000; // 5 MB

            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            });

        _provider = services.BuildServiceProvider();
    }

    public T GetService<T>() =>
        _provider.GetRequiredService<T>();
}
