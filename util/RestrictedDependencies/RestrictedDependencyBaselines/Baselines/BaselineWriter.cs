using System.Collections.Immutable;
using System.Text;
using Bit.RestrictedDependencyBaselines.Configuration;
using Bitwarden.Server.Sdk.RestrictedDependencies;

namespace Bit.RestrictedDependencyBaselines.Baselines;

/// <summary>
/// Writes the baseline files, and prunes the ones no longer backed by a restricted type, but only
/// when asked. Deleting a committed baseline is the one irreversible thing this tool does, and the
/// analyzer reads a deletion as a legitimate shrink, so a scan that merely failed to rediscover a
/// type must not be able to remove its budget silently.
/// </summary>
internal static class BaselineWriter
{
    public static void Write(string repoRoot, ImmutableArray<BudgetModel> files, bool prune, TextWriter output)
    {
        var directory = Path.Combine(repoRoot, RepoLocator.BaselinesDirectory);
        Directory.CreateDirectory(directory);

        var expected = files.Select(f => BudgetModel.FileNameFor(f.Type)).ToHashSet(StringComparer.Ordinal);
        var orphans = Directory.EnumerateFiles(directory, "*.json")
            .Where(p => !expected.Contains(Path.GetFileName(p)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        foreach (var orphan in orphans)
        {
            var name = Path.GetFileName(orphan);
            if (prune)
            {
                File.Delete(orphan);
                output.WriteLine($"removed orphaned {name}");
            }
            else
            {
                output.WriteLine($"kept {name}: no restricted type was found for it. Re-run with --prune to delete it.");
            }
        }

        foreach (var file in files)
        {
            var path = Path.Combine(directory, BudgetModel.FileNameFor(file.Type));
            EnsurePathWithinDirectory(path, directory);
            var content = file.Serialize();
            var summary = $"{file.Type}: {file.Usages.Sum(u => u.Count)} use(s) at {file.Usages.Count} site(s)";
            if (File.Exists(path) && File.ReadAllText(path) == content)
            {
                output.WriteLine($"unchanged {summary}");
                continue;
            }

            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            output.WriteLine($"wrote {summary}");
        }
    }

    private static void EnsurePathWithinDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path traversal detected.");
        }
    }
}
