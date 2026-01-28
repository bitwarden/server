using Bogus;

namespace Bit.Seeder.Data;

/// <summary>
/// Generates deterministic folder names using Bogus Commerce.Department().
/// Pre-generates a pool of business-themed names for consistent index-based access.
/// </summary>
internal sealed class FolderNameGenerator
{
    private const int _namePoolSize = 50;

    private readonly string[] _folderNames;

    public FolderNameGenerator(int seed)
    {
        var faker = new Faker { Random = new Randomizer(seed) };

        // Pre-generate business department names for determinism
        // Examples: "Automotive", "Home & Garden", "Sports", "Electronics", "Beauty"
        _folderNames = Enumerable.Range(0, _namePoolSize)
            .Select(_ => faker.Commerce.Department())
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Gets a folder name by index, wrapping around if index exceeds pool size.
    /// </summary>
    public string GetFolderName(int index) => _folderNames[index % _folderNames.Length];
}
