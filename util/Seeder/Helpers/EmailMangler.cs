namespace Bit.Seeder.Helpers;

/// <summary>
/// Mangles emails for test isolation and tracks the original → mangled mapping.
/// Each instance generates a unique prefix to prevent collisions across test runs.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// var mangler = new EmailMangler();
/// var mangledEmail = mangler.Mangle("user@example.com");  // "a1b2c3d4+user@example.com"
/// var map = mangler.GetMangleMap();  // { "user@example.com": "a1b2c3d4+user@example.com" }
/// </code>
/// </remarks>
public class EmailMangler
{
    private readonly MangleId _mangleId = new();

    private readonly Dictionary<string, string> _mangleMap = new();

    /// <summary>
    /// The unique prefix used for mangling in this instance.
    /// </summary>
    public string Prefix => _mangleId.Value;

    /// <summary>
    /// Mangles an email by prefixing it with the unique MangleId.
    /// Tracks the mapping for later retrieval.
    /// </summary>
    /// <param name="email">The original email address.</param>
    /// <returns>The mangled email (e.g., "a1b2c3d4+user@example.com").</returns>
    public string Mangle(string email)
    {
        var mangled = $"{_mangleId}+{email}";
        _mangleMap[email] = mangled;
        return mangled;
    }

    /// <summary>
    /// Returns a copy of the tracked mangle mappings (original → mangled).
    /// </summary>
    public Dictionary<string, string?> GetMangleMap()
    {
        return _mangleMap.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value);
    }
}
