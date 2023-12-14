using System.Diagnostics;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
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
    private const string TypeEntry = nameof(PersistedGrant.Type);
    private const string SubjectIdEntry = nameof(PersistedGrant.SubjectId);
    private const string SessionIdEntry = nameof(PersistedGrant.SessionId);
    private const string ClientIdEntry = nameof(PersistedGrant.ClientId);
    private const string DescriptionEntry = nameof(PersistedGrant.Description);
    private const string CreationTimeEntry = nameof(PersistedGrant.CreationTime);
    private const string ConsumedTimeEntry = nameof(PersistedGrant.ConsumedTime);
    private const string DataEntry = nameof(PersistedGrant.Data);

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
            var grantHashEntries = await redisDb.HashGetAllAsync(redisKey);

            if (grantHashEntries.Length == 0)
            {
                // It wasn't found, there is a chance is was instead stored in the fallback store
                _logger.LogWarning("Could not find grant in primary store, using fallback one.");
                return await _fallbackGrantStore.GetAsync(key);
            }

            // TODO: This goes to Redis twice for every GetAsync should we do this in a transaction
            // or we could directly store the expiry in the hash value.
            var expiry = await redisDb.KeyTimeToLiveAsync(redisKey);

            if (!expiry.HasValue)
            {
                throw new InvalidOperationException("Grants are always expected to be stored with an expiry.");
            }

            var persistedGrant = new PersistedGrant
            {
                Key = key,
            };

            foreach (var entry in grantHashEntries)
            {
                switch (entry.Name)
                {
                    case TypeEntry:
                        persistedGrant.Type = entry.Value;
                        break;
                    case SubjectIdEntry:
                        persistedGrant.SubjectId = entry.Value;
                        break;
                    case SessionIdEntry:
                        if (entry.Value.HasValue)
                        {
                            persistedGrant.SessionId = entry.Value;
                        }
                        break;
                    case ClientIdEntry:
                        persistedGrant.ClientId = entry.Value;
                        break;
                    case DescriptionEntry:
                        if (entry.Value.HasValue)
                        {
                            persistedGrant.Description = entry.Value;
                        }
                        break;
                    case CreationTimeEntry:
                        persistedGrant.CreationTime = new DateTime((long)entry.Value, DateTimeKind.Utc);
                        break;
                    case ConsumedTimeEntry:
                        if (entry.Value.HasValue)
                        {
                            persistedGrant.ConsumedTime = new DateTime((long)entry.Value, DateTimeKind.Utc);
                        }
                        break;
                    case DataEntry:
                        persistedGrant.Data = entry.Value;
                        break;
                }
            }

            Debug.Assert(persistedGrant.CreationTime != default, "CreationTime should have gotten a date");
            persistedGrant.Expiration = persistedGrant.CreationTime.Add(expiry.Value);

            return persistedGrant;
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
            var transaction = redisDb.CreateTransaction();

            var redisKey = CreateRedisKey(grant.Key);

            // Do not await transaction methods, the returned tasks only get completed once transaction.ExecuteAsync is called
            // Ref: https://stackexchange.github.io/StackExchange.Redis/Transactions.html#and-in-stackexchangeredis
            _ = transaction.HashSetAsync(redisKey, new HashEntry[]
            {
                new(TypeEntry, grant.Type),
                new(SubjectIdEntry, grant.SubjectId),
                new(SessionIdEntry, grant.SessionId != null ? grant.SessionId : RedisValue.EmptyString),
                new(ClientIdEntry, grant.ClientId),
                new(DescriptionEntry, grant.Description != null ? grant.Description : RedisValue.EmptyString),
                new(CreationTimeEntry, grant.CreationTime.Ticks),
                new(ConsumedTimeEntry, grant.ConsumedTime.HasValue ? grant.CreationTime.Ticks : RedisValue.EmptyString),
                new(DataEntry, grant.Data),
            });
            _ = transaction.KeyExpireAsync(redisKey, grant.Expiration.Value);
            await transaction.ExecuteAsync();
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
}
