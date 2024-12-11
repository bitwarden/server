using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.Utilities;

public static class DistributedCacheExtensions
{
    public static void Set<T>(this IDistributedCache cache, string key, T value)
    {
        Set(cache, key, value, new DistributedCacheEntryOptions());
    }

    public static void Set<T>(
        this IDistributedCache cache,
        string key,
        T value,
        DistributedCacheEntryOptions options
    )
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        cache.Set(key, bytes, options);
    }

    public static Task SetAsync<T>(this IDistributedCache cache, string key, T value)
    {
        return SetAsync(cache, key, value, new DistributedCacheEntryOptions());
    }

    public static Task SetAsync<T>(
        this IDistributedCache cache,
        string key,
        T value,
        DistributedCacheEntryOptions options
    )
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        return cache.SetAsync(key, bytes, options);
    }

    public static bool TryGetValue<T>(this IDistributedCache cache, string key, out T value)
    {
        var val = cache.Get(key);
        value = default;
        if (val == null)
            return false;
        try
        {
            value = JsonSerializer.Deserialize<T>(val);
        }
        catch
        {
            return false;
        }
        return true;
    }
}
