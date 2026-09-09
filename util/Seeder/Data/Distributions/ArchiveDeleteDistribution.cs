namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// The archived, both-archived-and-deleted, and deleted-only cipher index sets for a pool.
/// <see cref="Both"/> is a subset of <see cref="Archived"/>. <see cref="DeletedOnly"/> is disjoint
/// from <see cref="Archived"/>. <see cref="ArchivedOrder"/> holds the same indices as
/// <see cref="Archived"/>, ascending, for callers that need a stable position rather than just
/// membership.
/// </summary>
internal readonly record struct ArchiveDeleteSets(
    HashSet<int> Archived, HashSet<int> Both, HashSet<int> DeletedOnly, IReadOnlyList<int> ArchivedOrder);

/// <summary>
/// Computes archived/deleted lifecycle-state target counts and selects which cipher indices receive them.
/// </summary>
internal static class ArchiveDeleteDistribution
{
    /// <summary>
    /// Computes archived/deleted/both/delete-only target counts from density rates and caps.
    /// <paramref name="canArchive"/> gates only the archived (and therefore both) target to 0 — used by
    /// org ciphers, which need a user to attribute archiving to; personal ciphers always have one
    /// (their own owner), so it defaults to true.
    /// </summary>
    internal static (int ArchivedTarget, int DeletedTarget, int BothTarget, int DeletedOnlyTarget) ComputeTargets(
        int poolSize, double archivedRate, double deletedRate, double overlapRate, int maxArchived, int maxDeleted, bool canArchive = true)
    {
        var archivedTarget = canArchive && archivedRate > 0
            ? Math.Min((int)(poolSize * archivedRate), maxArchived)
            : 0;
        var deletedTarget = deletedRate > 0
            ? Math.Min((int)(poolSize * deletedRate), maxDeleted)
            : 0;
        var bothTarget = overlapRate > 0
            ? Math.Min(Math.Min((int)(poolSize * overlapRate), archivedTarget), maxDeleted)
            : 0;
        var deletedOnlyTarget = Math.Max(0, deletedTarget - bothTarget);

        return (archivedTarget, deletedTarget, bothTarget, deletedOnlyTarget);
    }

    /// <summary>
    /// Selects the archived/both/deleted-only index sets for a pool of <paramref name="poolSize"/>
    /// ciphers. Deleted-only positions are drawn from ciphers NOT selected as archived, so they can
    /// never collide with (or be starved by) the archived selection. Each set has exactly
    /// min(target, remaining pool) members. <c>ArchivedOrder[0]</c> is kept out of
    /// <see cref="ArchiveDeleteSets.Both"/> whenever there's room to (bothTarget &lt; archived.Count),
    /// so it's always safe to point a specific user (e.g. the org Owner) at it for manual QA.
    /// </summary>
    internal static ArchiveDeleteSets Select(int poolSize, int archivedTarget, int bothTarget, int deletedOnlyTarget)
    {
        var archived = EvenlySpacedIndices(poolSize, archivedTarget);
        var archivedSet = new HashSet<int>(archived);
        var bothCandidates = bothTarget < archived.Count ? archived.Skip(1).ToList() : archived;
        var bothPositions = EvenlySpacedIndices(bothCandidates.Count, bothTarget);
        var bothSet = new HashSet<int>(bothPositions.Select(position => bothCandidates[position]));
        var remaining = new List<int>(poolSize - archivedSet.Count);
        for (var i = 0; i < poolSize; i++)
        {
            if (!archivedSet.Contains(i))
            {
                remaining.Add(i);
            }
        }
        var deletedOnlyPositions = EvenlySpacedIndices(remaining.Count, deletedOnlyTarget);
        var deletedOnlySet = new HashSet<int>(deletedOnlyPositions.Select(position => remaining[position]));
        return new ArchiveDeleteSets(archivedSet, bothSet, deletedOnlySet, archived);
    }

    /// <summary>
    /// Maps each archived cipher index to a round-robin position in [0, userCount), one user per
    /// cipher, wrapping back to 0 after userCount assignments. Cycling by assignment count (not by
    /// the cipher index itself) guarantees every user slot is visited evenly.
    /// </summary>
    internal static Dictionary<int, int> AssignRoundRobinUserPositions(IReadOnlyList<int> archivedIndicesInOrder, int userCount)
    {
        var assignments = new Dictionary<int, int>(archivedIndicesInOrder.Count);
        for (var k = 0; k < archivedIndicesInOrder.Count; k++)
        {
            assignments[archivedIndicesInOrder[k]] = userCount > 0 ? k % userCount : 0;
        }

        return assignments;
    }

    /// <summary>
    /// Returns exactly min(target, poolSize) distinct, ascending indices in [0, poolSize), spread
    /// as evenly as possible via <c>k * poolSize / target</c> — the standard "distribute k items
    /// across n slots" formula. Guaranteed strictly increasing (no duplicates) whenever
    /// target &lt;= poolSize.
    /// </summary>
    internal static List<int> EvenlySpacedIndices(int poolSize, int target)
    {
        if (target <= 0 || poolSize <= 0)
        {
            return [];
        }
        target = Math.Min(target, poolSize);
        var result = new List<int>(target);
        for (var k = 0; k < target; k++)
        {
            result.Add((int)((long)k * poolSize / target));
        }
        return result;
    }
}
