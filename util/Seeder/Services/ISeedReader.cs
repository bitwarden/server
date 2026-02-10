namespace Bit.Seeder.Services;

/// <summary>
/// Reads seed data files from embedded JSON resources.
/// Internal to the Seeder library — seeds are pantry ingredients for Recipes, Steps, and Scenes.
/// </summary>
internal interface ISeedReader
{
    /// <summary>
    /// Reads and deserializes a seed file by name (without extension).
    /// Names use dot-separated paths: "ciphers.autofill-testing", "organizations.dunder-mifflin"
    /// </summary>
    T Read<T>(string seedName);

    /// <summary>
    /// Lists available seed file names (without extension).
    /// </summary>
    IReadOnlyList<string> ListAvailable();
}
