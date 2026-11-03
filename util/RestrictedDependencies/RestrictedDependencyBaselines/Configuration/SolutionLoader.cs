using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Bit.RestrictedDependencyBaselines.Configuration;

/// <summary>
/// Opens bitwarden-server.slnx as a Roslyn workspace and selects the projects Directory.Build.props
/// puts under RestrictedDependencyAnalysis: .csproj files under src/.
/// </summary>
internal static class SolutionLoader
{
    /// <summary>
    /// Registers the installed SDK's MSBuild. Must run before any MSBuild type is loaded, so it is
    /// kept out of line and called first from Program.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RegisterMsBuild()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    public static async Task<(MSBuildWorkspace Workspace, ImmutableArray<string> LoadDiagnostics)> OpenAsync(string repoRoot, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) => diagnostics.Add($"{e.Diagnostic.Kind}: {e.Diagnostic.Message}");

        try
        {
            await workspace.OpenSolutionAsync(Path.Combine(repoRoot, RepoLocator.SolutionFileName), cancellationToken: cancellationToken);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }

        return (workspace, [.. diagnostics]);
    }

    /// <summary>
    /// The projects the analyzer gates, in name order. Mirrors the RestrictedDependencyAnalysis condition
    /// in Directory.Build.props; keep the two in step.
    /// </summary>
    public static ImmutableArray<Project> InScopeProjects(Solution solution, string repoRoot) =>
    [
        .. solution.Projects
            .Where(p => p.FilePath is not null && p.Language == LanguageNames.CSharp)
            .Where(p => IsInScope(Path.GetRelativePath(repoRoot, p.FilePath!)))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
    ];

    public static bool IsInScope(string relativeProjectPath)
    {
        var normalized = relativeProjectPath.Replace('\\', '/');
        return normalized.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            && normalized.StartsWith("src/", StringComparison.Ordinal);
    }
}
