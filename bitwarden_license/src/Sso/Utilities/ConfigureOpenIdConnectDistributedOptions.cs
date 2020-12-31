using System;
using Bit.Core;
using Bit.Core.IdentityServer;
using IdentityServer4.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.Extensions.Options;

namespace Bit.Sso.Utilities
{
    public class ConfigureOpenIdConnectDistributedOptions : IPostConfigureOptions<CookieAuthenticationOptions>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly GlobalSettings _globalSettings;

        public ConfigureOpenIdConnectDistributedOptions(IHttpContextAccessor httpContextAccessor, GlobalSettings globalSettings)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _globalSettings = globalSettings;
        }

        public void PostConfigure(string name, CookieAuthenticationOptions options)
        {
            if (name != IdentityServer4.IdentityServerConstants.ExternalCookieAuthenticationScheme)
            {
                // Ignore
                return;
            }

            if (_globalSettings.SelfHosted || string.IsNullOrWhiteSpace(_globalSettings.IdentityServer?.RedisConnectionString))
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

            options.TicketDataFormat = new DistributedCacheTicketDataFormatter(_httpContextAccessor, name);
            //options.SessionStore = 
        }
    }
}
