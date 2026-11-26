# nullable enable

using System.Net;
using AngleSharp.Html.Parser;
using Bit.Core.Utilities;
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
                                                           // Adding headers to present source of traffic accurately
            client.DefaultRequestHeaders.Add("User-Agent", "Bitwarden");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        }).AddSsrfProtection(followRedirects: false);

        // The CreatePasswordUri handler wants similar headers as Icons but
        // needs to follow redirects to get the final URL.
        services.AddHttpClient("ChangePasswordUri", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 5000000; // 5 MB
                                                           // Adding headers to present source of traffic accurately
            client.DefaultRequestHeaders.Add("User-Agent", "Bitwarden");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        }).AddSsrfProtection();
    }

    public static void AddHtmlParsing(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlParser, HtmlParser>();
    }

    public static void AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IUriService, UriService>();
        services.AddSingleton<IDomainMappingService, DomainMappingService>();
        services.AddSingleton<IIconFetchingService, IconFetchingService>();
        services.AddSingleton<IChangePasswordUriService, ChangePasswordUriService>();
    }
}
