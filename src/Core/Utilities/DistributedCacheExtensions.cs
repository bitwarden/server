#nullable enable

using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.Utilities;

public static class DistributedCacheExtensions
{
    public static void Set<T>(this IDistributedCache cache, string key, T value,
        DistributedCacheEntryOptions options)
    {
        var bytes = Serialize(value);

        cache.Set(key, bytes, options);
    }

    public static async Task SetAsync<T>(this IDistributedCache cache, string key, T value,
        DistributedCacheEntryOptions options)
    {
        var bytes = Serialize(value);

        await cache.SetAsync(key, bytes, options);
    }

    public static bool TryGetValue<T>(this IDistributedCache cache, string key, out T? value)
    {
        var val = cache.Get(key);
        value = default;

        if (val == null) return false;

        try
        {
            value = Deserialize<T>(val);
        }
        catch
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets a value from cache, with a caller-supplied <paramref name="getMethod"/> (async) that is used if
    /// the value is not yet available.
    /// </summary>
    public static ValueTask<T> GetAsync<T>(this IDistributedCache cache, string key,
        Func<CancellationToken, ValueTask<T>> getMethod,
        DistributedCacheEntryOptions? options = null, CancellationToken cancellation = default)
        => GetAsyncShared<T>(cache, key, getMethod, options, cancellation);

    /// <summary>
    /// Gets a value from cache, with a caller-supplied <paramref name="getMethod"/> (sync) that is used if
    /// the value is not yet available.
    /// </summary>
    public static ValueTask<T> GetAsync<T>(this IDistributedCache cache, string key, Func<T> getMethod,
        DistributedCacheEntryOptions? options = null, CancellationToken cancellation = default)
        => GetAsyncShared<T>(cache, key, getMethod, options, cancellation);

    private static ValueTask<T> GetAsyncShared<T>(IDistributedCache cache, string key,
        Delegate getMethod,
        DistributedCacheEntryOptions? options, CancellationToken cancellation)
    {
        var pending = cache.GetAsync(key, cancellation);
        if (!pending.IsCompletedSuccessfully)
        {
            // async-result was not available immediately; go full-async
            return Awaited(cache, key, pending, getMethod, options, cancellation);
        }

        // GetAwaiter().GetResult() here is *not* "sync-over-async" - we've already
        // validated that this data was available synchronously, and we're eliding
        // the state machine overheads in the (hopefully high-hit-rate) success case
        var bytes = pending.GetAwaiter().GetResult();
        if (bytes is null)
        {
            // async-result was available but data is missing; go async for everything else
            return Awaited(cache, key, null, getMethod, options, cancellation);
        }

        // data was available synchronously; deserialize
        return new(Deserialize<T>(bytes));

        static async ValueTask<T> Awaited(
            IDistributedCache cache, // the underlying cache
            string key, // the key on the cache
            Task<byte[]?>? pending, // incomplete "get bytes" operation, if any
            Delegate getMethod, // the get-method supplied by the caller
            DistributedCacheEntryOptions? options, // cache expiration, etc
            CancellationToken cancellation)
        {
            byte[]? bytes;
            if (pending is not null)
            {
                bytes = await pending;
                if (bytes is not null)
                {
                    // data was available asynchronously
                    return Deserialize<T>(bytes);
                }
            }

            var result = getMethod switch
            {
                Func<CancellationToken, ValueTask<T>> get => await get(cancellation),
                Func<T> get => get(),
                _ => throw new ArgumentException(nameof(getMethod))
            };

            bytes = Serialize(result);

            if (options is null)
            {
                // not recommended; cache expiration should be considered
                // important, usually
                await cache.SetAsync(key, bytes, cancellation);
            }
            else
            {
                await cache.SetAsync(key, bytes, options, cancellation);
            }

            return result;
        }
    }

    private static T Deserialize<T>(byte[] bytes)
    {
        return JsonSerializer.Deserialize<T>(bytes)!;
    }

    private static byte[] Serialize<T>(T value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, value);
        return buffer.WrittenSpan.ToArray();
    }
}
