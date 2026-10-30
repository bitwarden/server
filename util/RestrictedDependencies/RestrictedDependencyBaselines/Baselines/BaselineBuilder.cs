using System.Collections.Immutable;
using Bit.RestrictedDependencyBaselines.Analysis;
using Bitwarden.Server.Sdk.RestrictedDependencies;

namespace Bit.RestrictedDependencyBaselines.Baselines;

/// <summary>
/// Merges observed uses into one baseline document per restricted type. Every restricted type gets
/// a file, even with no uses, because the analyzer treats a missing file as an error.
/// </summary>
internal static class BaselineBuilder
{
    public static ImmutableArray<BudgetModel> Build(SolutionAnalysis analysis)
    {
        var files = ImmutableArray.CreateBuilder<BudgetModel>();
        foreach (var type in analysis.RestrictedTypes)
        {
            var declared = analysis.DeclaredMembers.TryGetValue(type, out var members) ? members : [];

            // An excepted use is debt but not budget, so it never enters a baseline.
            var usages = analysis.Usages
                .Where(u => u.Type == type && !u.Excepted)
                .Select(u => new BudgetEntry(u.Kind, u.Member, u.Project, u.Site, u.Count, u.Tracked));

            files.Add(new BudgetModel(type, declared, usages));
        }

        return files.ToImmutable();
    }
}
