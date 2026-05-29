namespace Bit.Seeder.Services;

/// <summary>
/// Service for mangling strings to ensure test isolation and collision-free data.
/// </summary>
public interface IManglerService
{
    /// <summary>
    /// Mangles a string value for test isolation.
    /// Automatically tracks the original → mangled mapping.
    /// </summary>
    string Mangle(string value);

    /// <summary>
    /// Returns a copy of tracked mangle mappings (original → mangled).
    /// Used by Scenes to populate SceneResult.MangleMap.
    /// </summary>
    Dictionary<string, string?> GetMangleMap();

    /// <summary>
    /// Indicates whether mangling is enabled.
    /// </summary>
    bool IsEnabled { get; }
}
