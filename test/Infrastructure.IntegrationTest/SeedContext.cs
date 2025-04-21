using System.Diagnostics.CodeAnalysis;

namespace Bit.Infrastructure.IntegrationTest;

public class SeedContext
{
    private readonly Dictionary<string, object> _data = [];

    public bool TryGetValue<TValue>(string key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_data.TryGetValue(key, out var objValue) && objValue is TValue tValue)
        {
            value = tValue;
            return true;
        }

        value = default;
        return false;
    }

    public void Set(string key, object value)
    {
        if (!_data.TryAdd(key, value))
        {
            throw new InvalidOperationException($"'{key}' has already been set with data for this seed context.");
        }
    }
}
