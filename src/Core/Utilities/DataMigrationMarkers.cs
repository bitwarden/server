using Microsoft.Extensions.Logging;

namespace Bit.Core.Utilities;

/// <summary>
/// Each data migration has a no-op marker named <c>DataMigration_{Name}</c> at its position in every schema migration
/// chain, so self-hosted upgrades run it against the schema it was written for.
/// </summary>
public static class DataMigrationMarkers
{
    private const string _prefix = "_DataMigration_";

    /// <summary>The data migration a schema migration marks, or null if it isn't a marker.</summary>
    public static string? GetName(string migration)
    {
        var start = migration.IndexOf(_prefix, StringComparison.Ordinal);
        return start < 0 ? null : Path.GetFileNameWithoutExtension(migration[(start + _prefix.Length)..]);
    }

    /// <summary>
    /// Applies <paramref name="pending"/> schema migrations up to each marker, then runs its data migration with
    /// <paramref name="onDataMigration"/>. The marker is applied with the next segment if that returns true; otherwise,
    /// or with no handler, migrating stops at the marker.
    /// </summary>
    /// <returns>False if a segment failed to apply.</returns>
    public static bool Interleave(IReadOnlyList<string> pending, Func<IReadOnlyList<string>, bool> apply,
        Func<string, bool>? onDataMigration, ILogger logger)
    {
        var segment = new List<string>();
        foreach (var migration in pending)
        {
            if (GetName(migration) is not { } name)
            {
                segment.Add(migration);
                continue;
            }

            if (segment.Count > 0 && !apply(segment))
            {
                return false;
            }

            if (onDataMigration?.Invoke(name) != true)
            {
                logger.LogInformation(Constants.BypassFiltersEventId,
                    "Stopped at data migration {Name}; Admin runs it and the migrations after it when it starts.", name);
                return true;
            }

            segment = [migration];
        }

        return segment.Count == 0 || apply(segment);
    }
}
