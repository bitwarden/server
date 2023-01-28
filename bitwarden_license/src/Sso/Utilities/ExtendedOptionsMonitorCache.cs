using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Bit.Sso.Utilities;

public class ExtendedOptionsMonitorCache<TOptions> : IExtendedOptionsMonitorCache<TOptions> where TOptions : class
{
    private readonly ConcurrentDictionary<string, Lazy<TOptions>> _cache =
        new ConcurrentDictionary<string, Lazy<TOptions>>(StringComparer.Ordinal);

    public void AddOrUpdate(string name, TOptions options)
    {
        _cache.AddOrUpdate(name ?? Options.DefaultName, new Lazy<TOptions>(() => options),
            (string s, Lazy<TOptions> lazy) => new Lazy<TOptions>(() => options));
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public TOptions GetOrAdd(string name, Func<TOptions> createOptions)
    {
        return _cache.GetOrAdd(name ?? Options.DefaultName, new Lazy<TOptions>(createOptions)).Value;
    }

    public bool TryAdd(string name, TOptions options)
    {
        return _cache.TryAdd(name ?? Options.DefaultName, new Lazy<TOptions>(() => options));
    }

    public bool TryRemove(string name)
    {
        return _cache.TryRemove(name ?? Options.DefaultName, out _);
    }
}
