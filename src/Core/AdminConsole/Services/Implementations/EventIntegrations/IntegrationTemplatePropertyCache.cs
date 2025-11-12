using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Core.Services;

public abstract class IntegrationTemplatePropertyCache<TKey, TValue>(IMemoryCache memoryCache, TimeSpan cacheEntryTtl)
    where TKey : IEquatable<TKey>
{
    private readonly ConcurrentDictionary<TKey, AsyncLazy<TValue?>> _inflightLoads = new();

    protected abstract Task<TValue?> LoadValueFromRepositoryAsync(TKey key);

    public async Task<TValue?> GetAsync(TKey key)
    {
        // Cache hit - return cached
        if (memoryCache.TryGetValue(key, out TValue? cached))
        {
            return cached;
        }

        // Cache miss - Start or await an inflight request from the DB
        var lazy = _inflightLoads.GetOrAdd(key: key, value:
            new AsyncLazy<TValue?>(async () =>
            {
                try
                {
                    var loaded = await LoadValueFromRepositoryAsync(key);

                    if (loaded is not null)
                    {
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(cacheEntryTtl)
                            .SetSize(1);

                        memoryCache.Set(key, loaded, cacheOptions);
                    }

                    return loaded;
                }
                finally
                {
                    _inflightLoads.TryRemove(key: key, value: out _);
                }
            })
        );

        return await lazy.Value;
    }

    /// <summary>
    /// Helper to run a factory function only once per key.
    /// </summary>
    private sealed class AsyncLazy<T>
    {
        private readonly Lazy<Task<T>> _instance;

        public AsyncLazy(Func<Task<T>> factory)
        {
            _instance = new Lazy<Task<T>>(factory);
        }

        public Task<T> Value => _instance.Value;
    }
}
