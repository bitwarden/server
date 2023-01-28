using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Core.IdentityServer;

public class MemoryCacheTicketStore : ITicketStore
{
    private const string _keyPrefix = "auth-";
    private readonly IMemoryCache _cache;

    public MemoryCacheTicketStore()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = $"{_keyPrefix}{Guid.NewGuid()}";
        await RenewAsync(key, ticket);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new MemoryCacheEntryOptions();
        var expiresUtc = ticket.Properties.ExpiresUtc;
        if (expiresUtc.HasValue)
        {
            options.SetAbsoluteExpiration(expiresUtc.Value);
        }
        else
        {
            options.SetSlidingExpiration(TimeSpan.FromMinutes(15));
        }

        _cache.Set(key, ticket, options);

        return Task.FromResult(0);
    }

    public Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        _cache.TryGetValue(key, out AuthenticationTicket ticket);
        return Task.FromResult(ticket);
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.FromResult(0);
    }
}
