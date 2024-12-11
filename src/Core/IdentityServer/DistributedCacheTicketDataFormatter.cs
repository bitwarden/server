using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.IdentityServer;

public class DistributedCacheTicketDataFormatter : ISecureDataFormat<AuthenticationTicket>
{
    private const string CacheKeyPrefix = "ticket-data";

    private readonly IDistributedCache _distributedCache;
    private readonly IDataProtector _dataProtector;
    private readonly string _prefix;

    public DistributedCacheTicketDataFormatter(
        IDistributedCache distributedCache,
        IDataProtectionProvider dataProtectionProvider,
        string name
    )
    {
        _distributedCache = distributedCache;
        _dataProtector = dataProtectionProvider.CreateProtector(CacheKeyPrefix, name);
        _prefix = $"{CacheKeyPrefix}-{name}";
    }

    public string Protect(AuthenticationTicket data) => Protect(data, null);

    public string Protect(AuthenticationTicket data, string purpose)
    {
        var key = Guid.NewGuid().ToString();
        var cacheKey = $"{_prefix}-{purpose}-{key}";

        var expiresUtc = data.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(15);

        var options = new DistributedCacheEntryOptions();
        options.SetAbsoluteExpiration(expiresUtc);

        var ticket = TicketSerializer.Default.Serialize(data);
        _distributedCache.Set(cacheKey, ticket, options);

        return _dataProtector.Protect(key);
    }

    public AuthenticationTicket Unprotect(string protectedText) => Unprotect(protectedText, null);

    public AuthenticationTicket Unprotect(string protectedText, string purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return null;
        }

        // Decrypt the key and retrieve the data from the cache.
        var key = _dataProtector.Unprotect(protectedText);
        var cacheKey = $"{_prefix}-{purpose}-{key}";
        var ticket = _distributedCache.Get(cacheKey);

        if (ticket == null)
        {
            return null;
        }

        var data = TicketSerializer.Default.Deserialize(ticket);
        return data;
    }
}
