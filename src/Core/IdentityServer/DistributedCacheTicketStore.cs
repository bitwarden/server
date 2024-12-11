using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.IdentityServer;

public class DistributedCacheTicketStore : ITicketStore
{
    private const string KeyPrefix = "auth-";
    private readonly IDistributedCache _cache;

    public DistributedCacheTicketStore(IDistributedCache distributedCache)
    {
        _cache = distributedCache;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = $"{KeyPrefix}{Guid.NewGuid()}";
        await RenewAsync(key, ticket);

        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new DistributedCacheEntryOptions();
        var expiresUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(15);
        options.SetAbsoluteExpiration(expiresUtc);

        var val = SerializeToBytes(ticket);
        _cache.Set(key, val, options);

        return Task.FromResult(0);
    }

    public Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        AuthenticationTicket ticket;
        var bytes = _cache.Get(key);
        ticket = DeserializeFromBytes(bytes);

        return Task.FromResult(ticket);
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);

        return Task.FromResult(0);
    }

    private static byte[] SerializeToBytes(AuthenticationTicket source)
    {
        return TicketSerializer.Default.Serialize(source);
    }

    private static AuthenticationTicket DeserializeFromBytes(byte[] source)
    {
        return source == null ? null : TicketSerializer.Default.Deserialize(source);
    }
}
