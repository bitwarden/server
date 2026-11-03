using System.Collections.Immutable;
using System.Globalization;
using Bit.RestrictedDependencyBaselines.Configuration;
using Bitwarden.Server.Sdk.RestrictedDependencies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bit.RestrictedDependencyBaselines.Analysis;

/// <summary>
/// Runs the analyzer over every in-scope project exactly as the compiler would, with the
/// observation channel turned on in place of enforcement. Whatever made the analysis untrustworthy
/// is collected alongside the results rather than only logged, so a caller that is about to write
/// a baseline can decide what to do.
/// </summary>
internal static class ToolAnalysisRunner
{
    public static async Task<SolutionAnalysis> AnalyzeSolutionAsync(string repoRoot, TextWriter log, CancellationToken cancellationToken)
    {
        var (workspace, loadDiagnostics) = await SolutionLoader.OpenAsync(repoRoot, cancellationToken);
        using (workspace)
        {
            var degradations = ImmutableArray.CreateBuilder<string>();
            foreach (var diagnostic in loadDiagnostics)
            {
                log.WriteLine($"workspace: {diagnostic}");

                // The sqlproj is out of scope by design and always fails to open; anything else
                // means a project that should have been analyzed was not.
                if (!diagnostic.Contains(".sqlproj"))
                {
                    degradations.Add($"workspace: {diagnostic}");
                }
            }

            var projects = SolutionLoader.InScopeProjects(workspace.CurrentSolution, repoRoot);
            log.WriteLine($"Analyzing {projects.Length} project(s).");

            var compilations = new List<(Project Project, Compilation Compilation)>();
            foreach (var project in projects)
            {
                var compilation = await project.GetCompilationAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"'{project.Name}' produced no compilation.");
                compilations.Add((project, RestrictedTypeDiscovery.EnsureAttributeSource(compilation)));
            }

            var restrictedTypes = RestrictedTypeDiscovery.Find(compilations.Select(c => c.Compilation));
            log.WriteLine($"Restricted types: {(restrictedTypes.IsEmpty ? "none" : string.Join(", ", restrictedTypes))}");

            var reader = new ObservedUsageReader();
            var analyzerFailures = ImmutableArray.CreateBuilder<string>();

            // With nothing restricted there is nothing to observe, and running the analyzer with
            // an empty seed would put it back into enforce mode against baselines this tool
            // deliberately does not supply.
            if (restrictedTypes.IsEmpty)
            {
                return reader.ToAnalysis(restrictedTypes, degradations.ToImmutable(), analyzerFailures.ToImmutable());
            }

            foreach (var (project, compilation) in compilations)
            {
                var errors = CompileErrorCount(compilation, log, project, cancellationToken);
                if (errors > 0)
                {
                    degradations.Add($"'{project.Name}' has {errors} compile error(s)");
                }

                var uses = await ObserveAsync(repoRoot, project, compilation, restrictedTypes, reader, analyzerFailures, cancellationToken);
                log.WriteLine($"  {project.Name}: {uses} use(s)");
            }

            return reader.ToAnalysis(restrictedTypes, degradations.ToImmutable(), analyzerFailures.ToImmutable());
        }
    }

    private static async Task<int> ObserveAsync(
        string repoRoot,
        Project project,
        Compilation compilation,
        ImmutableArray<string> restrictedTypes,
        ObservedUsageReader reader,
        ImmutableArray<string>.Builder analyzerFailures,
        CancellationToken cancellationToken)
    {
        var analyzers = RestrictedDependencyAnalyzers(project);
        if (analyzers.IsEmpty)
        {
            throw new InvalidOperationException(
                $"'{project.Name}' does not reference the Bitwarden.Server.Sdk.RestrictedDependencies analyzer. This tool must run the "
                + "same analyzer the build runs; check the PackageReference in Directory.Build.props.");
        }

        // BW0017 is disabled by default, which is what keeps it free in a real build. Turning it
        // on is how this tool reads what the analyzer saw.
        var observing = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(ObservationConstants.DiagnosticId, ReportDiagnostic.Info)));

        var options = new AnalyzerOptions([], new ToolAnalyzerOptionsProvider(repoRoot, restrictedTypes));
        var withAnalyzers = new CompilationWithAnalyzers(
            observing,
            analyzers,
            new CompilationWithAnalyzersOptions(options, onAnalyzerException: null, concurrentAnalysis: true, logAnalyzerExecutionTime: false));

        // onAnalyzerException is null, which is Roslyn's swallowing mode: an action that throws
        // becomes an AD0001 in this array rather than propagating. Discarding the array would hide
        // a crash entirely, because the compilation-end action still runs and still reports rows.
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        foreach (var failure in diagnostics.Where(d => d.Id == "AD0001"))
        {
            analyzerFailures.Add($"'{project.Name}': {failure.GetMessage(CultureInfo.InvariantCulture)}");
        }

        // The assembly name, not the project name: it is what the analyzer keys a baseline row on.
        return reader.Read(compilation.AssemblyName ?? "unknown", diagnostics);
    }

    /// <summary>
    /// The analyzers the build would run that understand the observation channel. Taking them from
    /// the project's own analyzer references rather than constructing one is what makes "the same
    /// analyzer the build runs" true by construction.
    /// </summary>
    private static ImmutableArray<DiagnosticAnalyzer> RestrictedDependencyAnalyzers(Project project) =>
    [
        .. project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(LanguageNames.CSharp))
            .Where(analyzer => analyzer.SupportedDiagnostics.Any(d => d.Id == ObservationConstants.DiagnosticId)),
    ];

    /// <summary>
    /// A design-time compilation reports errors a real build does not, so errors are surfaced but
    /// never abort the analysis. The count is returned so a caller can weigh how much to trust it.
    /// </summary>
    private static int CompileErrorCount(Compilation compilation, TextWriter log, Project project, CancellationToken cancellationToken)
    {
        var errors = compilation.GetDiagnostics(cancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count == 0)
        {
            return 0;
        }

        log.WriteLine($"warning: '{project.Name}' has {errors.Count} compile error(s); uses in the affected code may be missing.");
        foreach (var error in errors.Take(3))
        {
            log.WriteLine($"    {error}");
        }

        return errors.Count;
    }
}
