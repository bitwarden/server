# nullable enable

using System.Net;
using AngleSharp.Html.Parser;
using Bit.Icons.Services;

namespace Bit.Icons.Extensions;

public static class ServiceCollectionExtension
{
    public static void ConfigureHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient("Icons", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 5000000; // 5 MB
                                                           // Let's add some headers to look like we're coming from a web browser request. Some websites
                                                           // will block our request without these.
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.8");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            client.DefaultRequestHeaders.Add("Pragma", "no-cache");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;" +
                "q=0.9,image/webp,image/apng,*/*;q=0.8");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        });
    }

    public static void AddHtmlParsing(this IServiceCollection services)
    {
        services.AddSingleton<HtmlParser>();
        services.AddSingleton<IHtmlParser>(s => s.GetRequiredService<HtmlParser>());
    }

    public static void AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IUriService, UriService>();
        services.AddSingleton<IDomainMappingService, DomainMappingService>();
        services.AddSingleton<IIconFetchingService, IconFetchingService>();
    }
}
