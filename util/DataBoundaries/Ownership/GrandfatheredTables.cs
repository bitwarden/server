using System.Collections.Immutable;

namespace Bit.DataBoundaries.Ownership;

/// <summary>
/// Table files exempt from the product-ownership rule because they predate it. The list may shrink and
/// must never grow: moving a table under an owning domain folder removes its entry.
/// </summary>
internal static class GrandfatheredTables
{
    public const string RepoRelativePath = "util/DataBoundaries/config/grandfathered-tables.txt";

    public static ImmutableHashSet<string> FromRepo(string repoRoot) =>
        FromFile(Path.Combine(repoRoot, RepoRelativePath));

    public static ImmutableHashSet<string> FromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Grandfathered table list not found: {path}", path);
        }

        return Parse(File.ReadAllLines(path));
    }

    /// <summary>
    /// Blank lines and # comments are ignored.
    /// </summary>
    public static ImmutableHashSet<string> Parse(IEnumerable<string> lines) =>
        lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Entries the change added, which is the direction the ratchet forbids.
    /// </summary>
    public static ImmutableArray<string> FindAddedEntries(
        IReadOnlySet<string> current,
        IReadOnlySet<string> baseline) =>
        [.. current
            .Where(entry => !baseline.Contains(entry))
            .OrderBy(entry => entry, StringComparer.Ordinal)];
}
