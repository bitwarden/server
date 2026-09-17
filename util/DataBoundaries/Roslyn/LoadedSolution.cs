using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Bit.DataBoundaries.Roslyn;

/// <summary>
/// Opens the repository's solution as a Roslyn workspace so column reads and writes can be
/// attributed by symbol rather than by text. Text matching cannot tell <c>user.Id</c> from
/// <c>organization.Id</c>, which is the whole reason this exists.
/// </summary>
internal sealed class LoadedSolution : IDisposable
{
    private readonly MSBuildWorkspace _workspace;

    internal LoadedSolution(MSBuildWorkspace workspace, ImmutableArray<string> loadDiagnostics)
    {
        _workspace = workspace;
        LoadDiagnostics = loadDiagnostics;
    }

    public Solution Solution => _workspace.CurrentSolution;

    /// <summary>
    /// Non-fatal load failures, kept rather than swallowed. Some are expected: the solution
    /// contains project types Roslyn cannot open, and a caller that hides these cannot tell an
    /// expected miss from a project that silently dropped out of the analysis.
    /// </summary>
    public ImmutableArray<string> LoadDiagnostics { get; }

    public void Dispose() => _workspace.Dispose();
}
