using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace Bit.Core.Utilities
{
    public static class DistributedCacheExtensions
    {
        public static void Set<T>(this IDistributedCache cache, string key, T value)
        {
            Set(cache, key, value, new DistributedCacheEntryOptions());
        }

        public static void Set<T>(this IDistributedCache cache, string key, T value,
            DistributedCacheEntryOptions options)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonHelpers.LegacySerialize(value));
            cache.Set(key, bytes, options);
        }

        public static Task SetAsync<T>(this IDistributedCache cache, string key, T value)
        {
            return SetAsync(cache, key, value, new DistributedCacheEntryOptions());
        }

        public static Task SetAsync<T>(this IDistributedCache cache, string key, T value,
            DistributedCacheEntryOptions options)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonHelpers.LegacySerialize(value));
            return cache.SetAsync(key, bytes, options);
        }

        public static bool TryGetValue<T>(this IDistributedCache cache, string key, out T value)
        {
            var val = cache.Get(key);
            value = default;
            if (val == null) return false;
            try
            {
                value = JsonHelpers.LegacyDeserialize<T>(Encoding.UTF8.GetString(val));
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
