namespace Bit.Seeder.Services;

/// <summary>
/// Reads seed data files from embedded JSON resources.
/// Seeds are pantry ingredients for Recipes, Steps, and Scenes.
/// </summary>
public interface ISeedReader
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

    /// <summary>
    /// Reads a bundled binary attachment body (under Seeds/attachments) by filename,
    /// e.g. "mock-seeder-data-bank-statement-1.pdf". Used to supply plaintext attachment bodies for seeding.
    /// </summary>
    byte[] ReadBytes(string fileName);
}
