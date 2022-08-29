using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.IdentityServer;

public class DistributedCacheCookieManager : ICookieManager
{
    private readonly ChunkingCookieManager _cookieManager;

    public DistributedCacheCookieManager()
    {
        _cookieManager = new ChunkingCookieManager();
    }

    private string CacheKeyPrefix => "cookie-data";

    public void AppendResponseCookie(HttpContext context, string key, string value, CookieOptions options)
    {
        var id = Guid.NewGuid().ToString();
        var cacheKey = GetKey(key, id);

        var expiresUtc = options.Expires ?? DateTimeOffset.UtcNow.AddMinutes(15);
        var cacheOptions = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(expiresUtc);

        var data = Encoding.UTF8.GetBytes(value);

        var cache = GetCache(context);
        cache.Set(cacheKey, data, cacheOptions);

        // Write the cookie with the identifier as the body
        _cookieManager.AppendResponseCookie(context, key, id, options);
    }

    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    {
        _cookieManager.DeleteCookie(context, key, options);
        var id = GetId(context, key);
        if (!string.IsNullOrWhiteSpace(id))
        {
            var cacheKey = GetKey(key, id);
            GetCache(context).Remove(cacheKey);
        }
    }

    public string GetRequestCookie(HttpContext context, string key)
    {
        var id = GetId(context, key);
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }
        var cacheKey = GetKey(key, id);
        return GetCache(context).GetString(cacheKey);
    }

    private IDistributedCache GetCache(HttpContext context) =>
        context.RequestServices.GetRequiredService<IDistributedCache>();

    private string GetKey(string key, string id) => $"{CacheKeyPrefix}-{key}-{id}";

    private string GetId(HttpContext context, string key) =>
        context.Request.Cookies.ContainsKey(key) ?
        context.Request.Cookies[key] : null;
}
