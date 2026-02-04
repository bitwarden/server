using System.Globalization;

namespace Bit.Seeder.Services;

/// <summary>
/// Scoped stateful implementation that mangles strings with a unique prefix.
/// Each instance generates its own MangleId and tracks all manglings in an internal map.
/// </summary>
public class ManglerService : IManglerService
{
    private readonly MangleId _mangleId = new();
    private readonly Dictionary<string, string> _mangleMap = new();

    public string Mangle(string value)
    {
        var atIndex = value.IndexOf('@');
        var mangled = atIndex >= 0
            ? $"{_mangleId}+{value[..atIndex]}{value[atIndex..]}"
            : $"{_mangleId}-{value}";

        _mangleMap[value] = mangled;
        return mangled;
    }

    public Dictionary<string, string?> GetMangleMap()
    {
        return _mangleMap.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value);
    }

    public bool IsEnabled => true;

    /// <summary>
    /// Helper for generating unique identifier suffixes to prevent collisions in test data.
    /// "Mangling" adds a random suffix to test data identifiers (usernames, emails, org names, etc.)
    /// to ensure uniqueness across multiple test runs and parallel test executions.
    /// </summary>
    private class MangleId
    {
        private readonly string _value = Random.Shared.NextInt64().ToString("x", CultureInfo.InvariantCulture)[..8];

        public override string ToString() => _value;
    }
}
