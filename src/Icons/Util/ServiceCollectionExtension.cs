# nullable enable

using System.Net;
using AngleSharp.Html.Parser;
using Bit.Core.Utilities;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        }).AddSsrfProtection(followRedirects: false);

        // The CreatePasswordUri handler wants similar headers as Icons to portray coming from a browser but
        // needs to follow redirects to get the final URL.
        services.AddHttpClient("ChangePasswordUri", client =>
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
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        }).AddSsrfProtection();
    }

    public static void AddHtmlParsing(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlParser, HtmlParser>();
    }

    /// <summary>
    /// Registers a separate <see cref="IMemoryCache"/> for each consumer, keyed by the names in
    /// <see cref="IconsCacheConstants"/>. Two calls to <c>AddMemoryCache</c> would not do this:
    /// it registers <see cref="IMemoryCache"/> with <c>TryAdd</c>, so the second call adds no
    /// second cache and only appends another options callback, leaving both consumers sharing one
    /// cache sized by whichever limit was configured last.
    /// </summary>
    public static void AddCaches(this IServiceCollection services, IconsSettings iconsSettings,
        ChangePasswordUriSettings changePasswordUriSettings)
    {
        services.TryAddKeyedSingleton<IMemoryCache>(IconsCacheConstants.IconsCacheName, (_, _) =>
            new MemoryCache(Options.Create(new MemoryCacheOptions
            {
                SizeLimit = iconsSettings.CacheSizeLimit
            })));
        services.TryAddKeyedSingleton<IMemoryCache>(IconsCacheConstants.ChangePasswordUriCacheName, (_, _) =>
            new MemoryCache(Options.Create(new MemoryCacheOptions
            {
                SizeLimit = changePasswordUriSettings.CacheSizeLimit
            })));
    }

    public static void AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IUriService, UriService>();
        services.AddSingleton<IDomainMappingService, DomainMappingService>();
        services.AddSingleton<IIconFetchingService, IconFetchingService>();
        services.AddSingleton<IChangePasswordUriService, ChangePasswordUriService>();
    }
}
