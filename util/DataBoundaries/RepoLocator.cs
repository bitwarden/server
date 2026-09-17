namespace Bit.DataBoundaries;

internal static class RepoLocator
{
    public static string Resolve(string? explicitRoot, string startDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var full = Path.GetFullPath(explicitRoot);
            if (!IsRepositoryRoot(full))
            {
                throw new DirectoryNotFoundException($"'{full}' is not a repository root: no .git entry found there.");
            }

            return full;
        }

        for (var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
             directory is not null;
             directory = directory.Parent)
        {
            if (IsRepositoryRoot(directory.FullName))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"No .git entry found walking up from '{startDirectory}'. Pass --repo-root to point at the repository root.");
    }

    // In a git worktree .git is a file containing a gitdir pointer, not a directory.
    private static bool IsRepositoryRoot(string directory)
    {
        var git = Path.Combine(directory, ".git");
        return Directory.Exists(git) || File.Exists(git);
    }
}
