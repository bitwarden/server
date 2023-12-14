using System.Diagnostics;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using MessagePack;
using StackExchange.Redis;

namespace Bit.Identity.IdentityServer;

/// <summary>
/// A <see cref="IPersistedGrantStore"/> that persists its grants on a Redis DB
/// </summary>
/// <remarks>
/// This store also allows a fallback to another store in the case that a key was not found
/// in the Redis DB or the Redis DB happens to be down.
/// </remarks>
public class RedisPersistedGrantStore : IPersistedGrantStore
{
    private static readonly MessagePackSerializerOptions _options = MessagePackSerializerOptions.Standard;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisPersistedGrantStore> _logger;
    private readonly IPersistedGrantStore _fallbackGrantStore;

    public RedisPersistedGrantStore(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisPersistedGrantStore> logger,
        IPersistedGrantStore fallbackGrantStore)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
        _fallbackGrantStore = fallbackGrantStore;

    }

    public Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        _logger.LogWarning("Redis does not implement 'GetAllAsync', Skipping.");
        return Task.FromResult(Enumerable.Empty<PersistedGrant>());
    }
    public async Task<PersistedGrant> GetAsync(string key)
    {
        try
        {
            if (!_connectionMultiplexer.IsConnected)
            {
                // Redis is down, fallback to using SQL table
                _logger.LogWarning("This is not connected, using fallback store to execute 'GetAsync' with {Key}.", key);
                return await _fallbackGrantStore.GetAsync(key);
            }

            var redisKey = CreateRedisKey(key);

            var redisDb = _connectionMultiplexer.GetDatabase();
            var redisValueAndExpiry = await redisDb.StringGetWithExpiryAsync(redisKey);

            if (!redisValueAndExpiry.Value.HasValue)
            {
                // It wasn't found, there is a chance is was instead stored in the fallback store
                _logger.LogWarning("Could not find grant in primary store, using fallback one.");
                return await _fallbackGrantStore.GetAsync(key);
            }

            Debug.Assert(redisValueAndExpiry.Expiry.HasValue, "Redis entry is expected to have an expiry.");

            var storablePersistedGrant = MessagePackSerializer.Deserialize<StorablePersistedGrant>(redisValueAndExpiry.Value, _options);

            return new PersistedGrant
            {
                Key = key,
                Type = storablePersistedGrant.Type,
                SubjectId = storablePersistedGrant.SubjectId,
                SessionId = storablePersistedGrant.SessionId,
                ClientId = storablePersistedGrant.ClientId,
                Description = storablePersistedGrant.Description,
                CreationTime = storablePersistedGrant.CreationTime,
                ConsumedTime = storablePersistedGrant.ConsumedTime,
                Data = storablePersistedGrant.Data,
                Expiration = storablePersistedGrant.CreationTime.Add(redisValueAndExpiry.Expiry.Value),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failure in 'GetAsync' using primary grant store, falling back.");
            return await _fallbackGrantStore.GetAsync(key);
        }
    }

    public Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        _logger.LogWarning("This does not implement 'RemoveAllAsync', Skipping.");
        return Task.CompletedTask;
    }

    // This method is not actually expected to get called and instead redis will just get rid of the expired items
    public async Task RemoveAsync(string key)
    {
        if (!_connectionMultiplexer.IsConnecting)
        {
            _logger.LogWarning("Redis is not connected, using fallback store to execute 'RemoveAsync', with {Key}", key);
            await _fallbackGrantStore.RemoveAsync(key);
        }

        var redisDb = _connectionMultiplexer.GetDatabase();
        await redisDb.KeyDeleteAsync(CreateRedisKey(key));
    }

    public async Task StoreAsync(PersistedGrant grant)
    {
        try
        {
            if (!_connectionMultiplexer.IsConnected)
            {
                _logger.LogWarning("Redis is not connected, using fallback store to execute 'StoreAsync', with {Key}", grant.Key);
                await _fallbackGrantStore.StoreAsync(grant);
            }

            if (!grant.Expiration.HasValue)
            {
                throw new ArgumentException("A PersistedGrant is always expected to include an expiration time.");
            }

            var redisDb = _connectionMultiplexer.GetDatabase();

            var redisKey = CreateRedisKey(grant.Key);

            var serializedGrant = MessagePackSerializer.Serialize(new StorablePersistedGrant
            {
                Type = grant.Type,
                SubjectId = grant.SubjectId,
                SessionId = grant.SessionId,
                ClientId = grant.ClientId,
                Description = grant.Description,
                CreationTime = grant.CreationTime,
                ConsumedTime = grant.ConsumedTime,
                Data = grant.Data,
            }, _options);

            await redisDb.StringSetAsync(redisKey, serializedGrant, grant.Expiration.Value - grant.CreationTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failure in 'StoreAsync' using primary grant store, falling back.");
            await _fallbackGrantStore.StoreAsync(grant);
        }
    }

    private static RedisKey CreateRedisKey(string key)
    {
        return $"pg:{key}";
    }

    // TODO: .NET 8 Make all properties required
    [MessagePackObject]
    public class StorablePersistedGrant
    {
        [Key(0)]
        public string Type { get; set; }

        [Key(1)]
        public string SubjectId { get; set; }

        [Key(2)]
        public string SessionId { get; set; }

        [Key(3)]
        public string ClientId { get; set; }

        [Key(4)]
        public string Description { get; set; }

        [Key(5)]
        public DateTime CreationTime { get; set; }

        [Key(6)]
        public DateTime? ConsumedTime { get; set; }

        [Key(7)]
        public string Data { get; set; }
    }
}
