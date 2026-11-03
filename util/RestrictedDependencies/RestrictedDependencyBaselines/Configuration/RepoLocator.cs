namespace Bit.RestrictedDependencyBaselines.Configuration;

/// <summary>
/// Finds the repository root, either from an explicit argument or by walking up from the current
/// directory to the first ancestor holding the solution file. The solution rather than <c>.git</c>,
/// so this resolves to the right place from inside a worktree.
/// </summary>
internal static class RepoLocator
{
    public const string SolutionFileName = "bitwarden-server.slnx";
    public const string BaselinesDirectory = "util/RestrictedDependencies/baselines";

    /// <summary>
    /// Resolves the repository root. Throws when neither the explicit root nor any ancestor of
    /// <paramref name="startDirectory"/> contains the solution file.
    /// </summary>
    public static string Resolve(string? explicitRoot, string startDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var full = Path.GetFullPath(explicitRoot);
            return IsRepositoryRoot(full)
                ? full
                : throw new DirectoryNotFoundException($"'{full}' does not contain {SolutionFileName}.");
        }

        for (var directory = new DirectoryInfo(Path.GetFullPath(startDirectory)); directory is not null; directory = directory.Parent)
        {
            if (IsRepositoryRoot(directory.FullName))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"No {SolutionFileName} found walking up from '{startDirectory}'. Pass --repo-root to point at the repository root.");
    }

    private static bool IsRepositoryRoot(string directory) =>
        File.Exists(Path.Combine(directory, SolutionFileName));
}
