using System.Collections.Immutable;

namespace Bit.RestrictedDependencyBaselines.Analysis;

/// <summary>
/// Everything one analysis of the solution produced, including whatever made it untrustworthy.
/// </summary>
/// <param name="RestrictedTypes">Fully qualified names of every type found carrying the attribute.</param>
/// <param name="Usages">Every observed use, across every in-scope project.</param>
/// <param name="DeclaredMembers">
/// The members each sealed restricted type declares, keyed by type. What the baseline's
/// <c>declaredMembers</c> is written from.
/// </param>
/// <param name="Degradations">
/// Reasons the analysis may have missed uses: a project that would not load, or one that did not
/// compile. Advisory: a design-time build reports errors a real build does not.
/// </param>
/// <param name="AnalyzerFailures">
/// AD0001 diagnostics: the analyzer itself threw. Unlike a degradation this is never expected, and
/// the uses the crashed action would have recorded are simply absent.
/// </param>
internal sealed record SolutionAnalysis(
    ImmutableArray<string> RestrictedTypes,
    ImmutableArray<ObservedUsage> Usages,
    ImmutableDictionary<string, ImmutableArray<string>> DeclaredMembers,
    ImmutableArray<string> Degradations,
    ImmutableArray<string> AnalyzerFailures);
