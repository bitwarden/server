using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bit.Core.IdentityServer;

public class ConfigureOpenIdConnectDistributedOptions : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly IdentityServerOptions _idsrv;
    private readonly IDistributedCache _distributedCache;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public ConfigureOpenIdConnectDistributedOptions(
        [FromKeyedServices("persistent")]
        IDistributedCache distributedCache,
        IDataProtectionProvider dataProtectionProvider,
        IdentityServerOptions idsrv)
    {
        _idsrv = idsrv;
        _distributedCache = distributedCache;
        _dataProtectionProvider = dataProtectionProvider;
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
        options.TicketDataFormat = new DistributedCacheTicketDataFormatter(_distributedCache, _dataProtectionProvider, name);
        options.SessionStore = new DistributedCacheTicketStore(_distributedCache);
    }
}
