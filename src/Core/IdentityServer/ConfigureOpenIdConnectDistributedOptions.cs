using Bit.Core.Settings;
using IdentityServer4.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

namespace Bit.Core.IdentityServer;

public class ConfigureOpenIdConnectDistributedOptions : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly IdentityServerOptions _idsrv;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly GlobalSettings _globalSettings;

    public ConfigureOpenIdConnectDistributedOptions(IHttpContextAccessor httpContextAccessor, GlobalSettings globalSettings,
        IdentityServerOptions idsrv)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _globalSettings = globalSettings;
        _idsrv = idsrv;
    }

    public void PostConfigure(string name, CookieAuthenticationOptions options)
    {
        options.CookieManager = new DistributedCacheCookieManager();

        if (name != AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
        {
            // Ignore
            return;
        }

        options.Cookie.Name = AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = _idsrv.Authentication.CookieSameSiteMode;
        options.TicketDataFormat = new DistributedCacheTicketDataFormatter(_httpContextAccessor, name);

        if (string.IsNullOrWhiteSpace(_globalSettings.IdentityServer?.RedisConnectionString))
        {
            options.SessionStore = new MemoryCacheTicketStore();
        }
        else
        {
            var redisOptions = new RedisCacheOptions
            {
                Configuration = _globalSettings.IdentityServer.RedisConnectionString,
            };
            options.SessionStore = new RedisCacheTicketStore(redisOptions);
        }
    }
}
