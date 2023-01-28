using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.IdentityServer;

public class DistributedCacheTicketDataFormatter : ISecureDataFormat<AuthenticationTicket>
{
    private readonly IHttpContextAccessor _httpContext;
    private readonly string _name;

    public DistributedCacheTicketDataFormatter(IHttpContextAccessor httpContext, string name)
    {
        _httpContext = httpContext;
        _name = name;
    }

    private string CacheKeyPrefix => "ticket-data";
    private IDistributedCache Cache => _httpContext.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
    private IDataProtector Protector => _httpContext.HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector(CacheKeyPrefix, _name);

    public string Protect(AuthenticationTicket data) => Protect(data, null);
    public string Protect(AuthenticationTicket data, string purpose)
    {
        var key = Guid.NewGuid().ToString();
        var cacheKey = $"{CacheKeyPrefix}-{_name}-{purpose}-{key}";

        var expiresUtc = data.Properties.ExpiresUtc ??
            DateTimeOffset.UtcNow.AddMinutes(15);

        var options = new DistributedCacheEntryOptions();
        options.SetAbsoluteExpiration(expiresUtc);

        var ticket = TicketSerializer.Default.Serialize(data);
        Cache.Set(cacheKey, ticket, options);

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
        var ticket = Cache.Get(cacheKey);

        if (ticket == null)
        {
            return null;
        }

        var data = TicketSerializer.Default.Deserialize(ticket);
        return data;
    }
}
