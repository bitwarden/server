using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework;

public class EntityFrameworkCache : IDistributedCache
{
#if DEBUG
    // Used for debugging in tests
    public Task? scanTask;
#endif
    private static readonly TimeSpan _defaultSlidingExpiration = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan _expiredItemsDeletionInterval = TimeSpan.FromMinutes(30);
    private DateTimeOffset _lastExpirationScan;
    private readonly Action _deleteExpiredCachedItemsDelegate;
    private readonly object _mutex = new();
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TimeProvider _timeProvider;

    public EntityFrameworkCache(
        IServiceScopeFactory serviceScopeFactory,
        TimeProvider? timeProvider = null)
    {
        _deleteExpiredCachedItemsDelegate = DeleteExpiredCacheItems;
        _serviceScopeFactory = serviceScopeFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public byte[]? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var cache = dbContext.Cache
            .Where(c => c.Id == key && _timeProvider.GetUtcNow().UtcDateTime <= c.ExpiresAtTime)
            .SingleOrDefault();

        if (cache == null)
        {
            return null;
        }

        if (UpdateCacheExpiration(cache))
        {
            dbContext.SaveChanges();
        }

        ScanForExpiredItemsIfRequired();
        return cache?.Value;
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        token.ThrowIfCancellationRequested();

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var cache = await dbContext.Cache
            .Where(c => c.Id == key && _timeProvider.GetUtcNow().UtcDateTime <= c.ExpiresAtTime)
            .SingleOrDefaultAsync(cancellationToken: token);

        if (cache == null)
        {
            return null;
        }

        if (UpdateCacheExpiration(cache))
        {
            await dbContext.SaveChangesAsync(token);
        }

        ScanForExpiredItemsIfRequired();
        return cache?.Value;
    }

    public void Refresh(string key) => Get(key);

    public Task RefreshAsync(string key, CancellationToken token = default) => GetAsync(key, token);

    public void Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        using var scope = _serviceScopeFactory.CreateScope();
        GetDatabaseContext(scope).Cache
            .Where(c => c.Id == key)
            .ExecuteDelete();

        ScanForExpiredItemsIfRequired();
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        token.ThrowIfCancellationRequested();
        using var scope = _serviceScopeFactory.CreateScope();
        await GetDatabaseContext(scope).Cache
            .Where(c => c.Id == key)
            .ExecuteDeleteAsync(cancellationToken: token);

        ScanForExpiredItemsIfRequired();
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var cache = dbContext.Cache.Find(key);
        var insert = cache == null;
        cache = SetCache(cache, key, value, options);
        if (insert)
        {
            dbContext.Add(cache);
        }

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException e)
        {
            if (IsDuplicateKeyException(e))
            {
                // There is a possibility that multiple requests can try to add the same item to the cache, in
                // which case we receive a 'duplicate key' exception on the primary key column.
            }
            else
            {
                throw;
            }
        }

        ScanForExpiredItemsIfRequired();
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        token.ThrowIfCancellationRequested();

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var cache = await dbContext.Cache.FindAsync(new object[] { key }, cancellationToken: token);
        var insert = cache == null;
        cache = SetCache(cache, key, value, options);
        if (insert)
        {
            await dbContext.AddAsync(cache, token);
        }

        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateException e)
        {
            if (IsDuplicateKeyException(e))
            {
                // There is a possibility that multiple requests can try to add the same item to the cache, in
                // which case we receive a 'duplicate key' exception on the primary key column.
            }
            else
            {
                throw;
            }
        }

        ScanForExpiredItemsIfRequired();
    }

    private Cache SetCache(Cache? cache, string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        // resolve options
        if (!options.AbsoluteExpiration.HasValue &&
            !options.AbsoluteExpirationRelativeToNow.HasValue &&
            !options.SlidingExpiration.HasValue)
        {
            options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = _defaultSlidingExpiration
            };
        }

        if (cache == null)
        {
            // do an insert
            cache = new Cache { Id = key };
        }

        var slidingExpiration = (long?)options.SlidingExpiration?.TotalSeconds;

        // calculate absolute expiration
        DateTime? absoluteExpiration = null;
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
        }
        else if (options.AbsoluteExpiration.HasValue)
        {
            if (options.AbsoluteExpiration.Value <= utcNow)
            {
                throw new InvalidOperationException("The absolute expiration value must be in the future.");
            }

            absoluteExpiration = options.AbsoluteExpiration.Value.UtcDateTime;
        }

        // set values on cache
        cache.Value = value;
        cache.SlidingExpirationInSeconds = slidingExpiration;
        cache.AbsoluteExpiration = absoluteExpiration;
        if (slidingExpiration.HasValue)
        {
            cache.ExpiresAtTime = utcNow.AddSeconds(slidingExpiration.Value);
        }
        else if (absoluteExpiration.HasValue)
        {
            cache.ExpiresAtTime = absoluteExpiration.Value;
        }
        else
        {
            throw new InvalidOperationException("Either absolute or sliding expiration needs to be provided.");
        }

        return cache;
    }

    private bool UpdateCacheExpiration(Cache cache)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (cache.SlidingExpirationInSeconds.HasValue && (cache.AbsoluteExpiration.HasValue || cache.AbsoluteExpiration != cache.ExpiresAtTime))
        {
            if (cache.AbsoluteExpiration.HasValue && (cache.AbsoluteExpiration.Value - utcNow).TotalSeconds <= cache.SlidingExpirationInSeconds)
            {
                cache.ExpiresAtTime = cache.AbsoluteExpiration.Value;
            }
            else
            {
                cache.ExpiresAtTime = utcNow.AddSeconds(cache.SlidingExpirationInSeconds.Value);
            }
            return true;
        }
        return false;
    }

    private void ScanForExpiredItemsIfRequired()
    {
        lock (_mutex)
        {
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            if ((utcNow - _lastExpirationScan) > _expiredItemsDeletionInterval)
            {
                _lastExpirationScan = utcNow;
#if DEBUG
                scanTask =
#endif
                Task.Run(_deleteExpiredCachedItemsDelegate);
            }
        }
    }

    private void DeleteExpiredCacheItems()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        GetDatabaseContext(scope).Cache
            .Where(c => _timeProvider.GetUtcNow().UtcDateTime > c.ExpiresAtTime)
            .ExecuteDelete();
    }

    private DatabaseContext GetDatabaseContext(IServiceScope serviceScope)
    {
        return serviceScope.ServiceProvider.GetRequiredService<DatabaseContext>();
    }

    private static bool IsDuplicateKeyException(DbUpdateException e)
    {
        // MySQL
        if (e.InnerException is MySqlConnector.MySqlException myEx)
        {
            return myEx.ErrorCode == MySqlConnector.MySqlErrorCode.DuplicateKeyEntry;
        }
        // SQL Server
        else if (e.InnerException is Microsoft.Data.SqlClient.SqlException msEx)
        {
            return msEx.Errors != null &&
                msEx.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(error => error.Number == 2627);
        }
        // Postgres
        else if (e.InnerException is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == "23505";
        }
        // Sqlite
        else if (e.InnerException is Microsoft.Data.Sqlite.SqliteException liteEx)
        {
            return liteEx.SqliteErrorCode == 19 && liteEx.SqliteExtendedErrorCode == 1555;
        }
        return false;
    }
}
