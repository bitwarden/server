using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;

namespace Bit.Infrastructure.EntityFramework;

public class EntityFrameworkCache : IDistributedCache
{
    private readonly ISystemClock _systemClock;
    private readonly TimeSpan _expiredItemsDeletionInterval = TimeSpan.FromMinutes(30);
    private DateTimeOffset _lastExpirationScan;
    private readonly Action _deleteExpiredCachedItemsDelegate;
    private readonly TimeSpan _defaultSlidingExpiration = TimeSpan.FromMinutes(20);
    private readonly object _mutex = new();

    public IServiceScopeFactory ServiceScopeFactory { get; }

    public EntityFrameworkCache(IServiceScopeFactory serviceScopeFactory)
    {
        _systemClock = new SystemClock();
        _deleteExpiredCachedItemsDelegate = DeleteExpiredCacheItems;
        ServiceScopeFactory = serviceScopeFactory;
    }

    public byte[] Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        using var scope = ServiceScopeFactory.CreateScope();
        var cache = GetDatabaseContext(scope).Cache
            .Where(c => c.Id == key && c.ExpiresAtTime >= _systemClock.UtcNow)
            .SingleOrDefault();
        ScanForExpiredItemsIfRequired();
        return cache?.Value;
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        token.ThrowIfCancellationRequested();
        using var scope = ServiceScopeFactory.CreateScope();
        var cache = await GetDatabaseContext(scope).Cache
            .Where(c => c.Id == key && c.ExpiresAtTime >= _systemClock.UtcNow)
            .SingleOrDefaultAsync(cancellationToken: token);
        ScanForExpiredItemsIfRequired();
        return cache?.Value;
    }

    public void Refresh(string key) => throw new NotImplementedException();
    public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotImplementedException();
    public void Remove(string key) => throw new NotImplementedException();
    public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotImplementedException();
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotImplementedException();
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new NotImplementedException();

    private void ScanForExpiredItemsIfRequired()
    {
        lock (_mutex)
        {
            var utcNow = _systemClock.UtcNow;
            if ((utcNow - _lastExpirationScan) > _expiredItemsDeletionInterval)
            {
                _lastExpirationScan = utcNow;
                Task.Run(_deleteExpiredCachedItemsDelegate);
            }
        }
    }

    private void DeleteExpiredCacheItems()
    {
        _databaseContext.Cache
            .Where(c => c.ExpiresAtTime < _systemClock.UtcNow)
            .ExecuteDelete();
    }

    private DatabaseContext GetDatabaseContext(IServiceScope serviceScope)
    {
        return serviceScope.ServiceProvider.GetRequiredService<DatabaseContext>();
    }
}
