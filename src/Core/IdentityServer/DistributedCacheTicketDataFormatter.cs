using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Bit.Core.IdentityServer
{
    public class DistributedCacheTicketDataFormatter : ISecureDataFormat<AuthenticationTicket>
    {
        private readonly IHttpContextAccessor _httpContext;
        private readonly string _name;

        public DistributedCacheTicketDataFormatter(IHttpContextAccessor httpContext, string name)
        {
            _httpContext = httpContext;
            _name = name;
        }

        private string CacheKeyPrefix => "DistributedCacheTicketDataFormatter";

        private IDistributedCache Cache => _httpContext.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
        private IDataProtector Protector => _httpContext.HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(CacheKeyPrefix, _name);

        public string Protect(AuthenticationTicket data) => Protect(data, null);
        public string Protect(AuthenticationTicket data, string purpose)
        {
            var key = Guid.NewGuid().ToString();
            var cacheKey = $"{CacheKeyPrefix}-{_name}-{purpose}-{key}";

            // TODO: Handle custom serialization.
            //var authItems = data.Properties.Items;
            //var princ = data.Principal.WriteTo;
            //var principle = new System.Security.Claims.ClaimsPrincipal();
            //data.AuthenticationScheme

            var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            });

            var options = new DistributedCacheEntryOptions();
            options.SetSlidingExpiration(TimeSpan.FromMinutes(60));
            
            Cache.SetString(cacheKey, json, options);

            return Protector.Protect(key);
        }

        public AuthenticationTicket Unprotect(string protectedText) => Unprotect(protectedText, null);
        public AuthenticationTicket Unprotect(string protectedText, string purpose)
        {
            if (string.IsNullOrWhiteSpace(protectedText))
            {
                return null;
            }

            // Decrypt the key and retrieve the data from the cache.
            var key = Protector.Unprotect(protectedText);
            var cacheKey = $"{CacheKeyPrefix}-{_name}-{purpose}-{key}";
            var json = Cache.GetString(cacheKey);

            if (json == null)
            {
                return null;
            }

            var ticket = JsonConvert.DeserializeObject<AuthenticationTicket>(json);
            return ticket;
        }
    }
}
