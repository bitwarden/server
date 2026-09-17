using System.Collections.Immutable;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Bit.DataBoundaries.Roslyn;

internal static class SolutionLoader
{
    public const string SolutionFileName = "bitwarden-server.slnx";

    /// <summary>
    /// Project types Roslyn cannot open. They are excluded from the analysis, not from the
    /// solution, so their failure to load is expected rather than a problem to report.
    /// </summary>
    private static readonly string[] _unloadableProjectSuffixes = [".sqlproj"];

    private static readonly string[] _excludedProjectNames = ["AppHost", "Sql"];

    /// <summary>
    /// Must run before any MSBuild type loads, which is why <c>Program.Main</c> calls it first
    /// rather than the command that needs it.
    /// </summary>
    public static void RegisterMsBuild()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    public static async Task<LoadedSolution> OpenAsync(string repoRoot, CancellationToken cancellationToken)
    {
        var solutionPath = Path.Combine(repoRoot, SolutionFileName);
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution not found at '{solutionPath}'.", solutionPath);
        }

        var diagnostics = new List<string>();
        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) => diagnostics.Add($"{e.Diagnostic.Kind}: {e.Diagnostic.Message}");

        try
        {
            await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }

        return new LoadedSolution(workspace, [.. diagnostics]);
    }

    /// <summary>
    /// Projects whose C# is worth analysing. Test, tooling and migration projects are excluded
    /// because a reference from them is not a product dependency on another boundary's data.
    /// </summary>
    public static ImmutableArray<Project> AnalyzableProjects(Solution solution) =>
    [
        .. solution.Projects
            .Where(p => p.FilePath is not null)
            .Where(p => p.Language == LanguageNames.CSharp)
            .Where(p => !_unloadableProjectSuffixes.Any(s => p.FilePath!.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            .Where(p => !_excludedProjectNames.Contains(p.Name))
            .Where(p => !IsExcludedPath(p.FilePath!))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
    ];

    private static bool IsExcludedPath(string projectPath)
    {
        var normalized = projectPath.Replace('\\', '/');
        return normalized.Contains("/util/", StringComparison.Ordinal)
            || normalized.Contains("/perf/", StringComparison.Ordinal)
            || normalized.Contains("/test/", StringComparison.Ordinal);
    }
}
