using System.Collections.Immutable;
using System.Globalization;
using Bitwarden.Server.Sdk.RestrictedDependencies;
using Microsoft.CodeAnalysis;

namespace Bit.RestrictedDependencyBaselines.Analysis;

/// <summary>
/// Turns the analyzer's BW0017 rows into what this tool needs. Compliant code produces no
/// diagnostic, so the uses that make up a baseline arrive through this channel rather than through
/// the rules; that is what lets the tool run the same analyzer the build runs instead of a second
/// scanner that could disagree with it.
/// </summary>
internal sealed class ObservedUsageReader
{
    private readonly List<ObservedUsage> _usages = [];
    private readonly Dictionary<string, SortedSet<string>> _declaredMembers = new(StringComparer.Ordinal);

    /// <summary>
    /// Reads one project's diagnostics, returning how many uses it contributed. Exception and
    /// restricted-type rows are reporting data the baseline does not carry, so they are skipped.
    /// </summary>
    public int Read(string project, ImmutableArray<Diagnostic> diagnostics)
    {
        var uses = 0;
        foreach (var diagnostic in diagnostics.Where(d => d.Id == ObservationConstants.DiagnosticId))
        {
            var row = diagnostic.Properties;
            var type = Required(row, ObservationConstants.Type);
            switch (Required(row, ObservationConstants.Row))
            {
                case ObservationConstants.UsageRow:
                    _usages.Add(ReadUsage(project, type, row));
                    uses++;
                    break;

                case ObservationConstants.DeclaredMemberRow:
                    Declared(type).Add(Required(row, ObservationConstants.Member));
                    break;
            }
        }

        return uses;
    }

    public SolutionAnalysis ToAnalysis(
        ImmutableArray<string> restrictedTypes,
        ImmutableArray<string> degradations,
        ImmutableArray<string> analyzerFailures) =>
        new(
            restrictedTypes,
            [.. _usages],
            _declaredMembers.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray(), StringComparer.Ordinal),
            degradations,
            analyzerFailures);

    private static ObservedUsage ReadUsage(string project, string type, ImmutableDictionary<string, string?> row)
    {
        var kindToken = Required(row, ObservationConstants.UsageKind);
        if (!DependencyUsageTypeExtensions.TryParse(kindToken, out var kind))
        {
            throw new InvalidOperationException($"The analyzer reported an unknown access shape '{kindToken}'.");
        }

        return new ObservedUsage(
            project,
            type,
            kind,
            Optional(row, ObservationConstants.Member),
            Required(row, ObservationConstants.Site),
            int.Parse(Required(row, ObservationConstants.Count), CultureInfo.InvariantCulture),
            Required(row, ObservationConstants.File),
            Flag(row, ObservationConstants.Excepted),
            Flag(row, ObservationConstants.Tracked));
    }

    private SortedSet<string> Declared(string type)
    {
        if (!_declaredMembers.TryGetValue(type, out var members))
        {
            members = new SortedSet<string>(StringComparer.Ordinal);
            _declaredMembers[type] = members;
        }

        return members;
    }

    /// <summary>
    /// A property the row must carry. A missing one means the analyzer and this reader disagree
    /// about the wire format, which would otherwise show up as a silently incomplete baseline.
    /// </summary>
    private static string Required(ImmutableDictionary<string, string?> row, string key) =>
        row.TryGetValue(key, out var value) && value is not null
            ? value
            : throw new InvalidOperationException($"A {ObservationConstants.DiagnosticId} row is missing '{key}'. The analyzer package and this tool are out of step.");

    private static string? Optional(ImmutableDictionary<string, string?> row, string key) =>
        row.TryGetValue(key, out var value) ? value : null;

    private static bool Flag(ImmutableDictionary<string, string?> row, string key) =>
        Required(row, key) == ObservationConstants.ToToken(true);
}
