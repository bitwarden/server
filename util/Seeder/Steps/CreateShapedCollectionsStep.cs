using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates shared collections with long-tailed target sizes and grants them to groups and members.
/// </summary>
/// <remarks>
/// <para>Replaces <see cref="CreateCollectionsStep"/> when a preset has an <c>accessShape</c> block.
/// Report rows come from (group members × collection items) per grant, so this step controls the pairing:</para>
/// <list type="bullet">
/// <item><description>Hot pairs: each hot group gets dedicated large collections of exact sizes.</description></item>
/// <item><description>The everyone group gets a few tiny collections (huge reach, few items).</description></item>
/// <item><description>Remaining group grants go to random collections, favoring small groups (members^-bias).</description></item>
/// <item><description>Direct grants favor bigger collections ((items+1)^bias) and heavy-tailed members.</description></item>
/// </list>
/// <para>Target sizes are stored in <see cref="EntityRegistry.CollectionTargetSizes"/>; <see cref="GenerateCiphersStep"/> fills them.</para>
/// </remarks>
internal sealed class CreateShapedCollectionsStep(
    int count,
    SeedPresetAccessShape shape,
    Distribution<PermissionWeight> permissions,
    int seed) : IStep
{
    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var orgKey = context.RequireOrgKey();
        var progress = context.GetProgress();
        var random = new Random(seed + 1);
        var registry = context.Registry;

        var collectionShape = shape.Collections ?? new SeedPresetShapeCollections();
        var grantShape = shape.GroupGrants ?? new SeedPresetShapeGroupGrants();
        var directShape = shape.DirectGrants ?? new SeedPresetShapeDirectGrants();
        var hotPairs = grantShape.HotPairs ?? [];

        // Sizes: pinned hot collections first (department of the first hot group granted each), then histogram
        // draws scaled to the remaining total
        var hotCollections = grantShape.HotCollections ?? [];
        var pinned = hotCollections.Select((size, i) => (
            Group: hotPairs.FirstOrDefault(p => p.Collections.Contains(i))?.Group ?? -1,
            size)).ToList();
        var drawnCount = count - pinned.Count;
        if (drawnCount < 0)
        {
            throw new InvalidOperationException($"collections.count ({count}) is smaller than the {pinned.Count} hot-pair collections.");
        }

        var buckets = collectionShape.Sizes ?? [new SeedPresetSizeBucket { Min = 1, Max = 1, Weight = 1 }];
        var drawn = new SizeHistogram(buckets.Select(b => (b.Min, b.Max, b.Weight))).Draw(drawnCount, random);
        if (collectionShape.TotalAssignments is { } totalAssignments)
        {
            SizeHistogram.ScaleTo(drawn, totalAssignments - pinned.Sum(p => p.size), buckets.Max(b => b.Max));
        }

        var sizes = pinned.Select(p => p.size).Concat(drawn).ToArray();

        progress?.Report(new PhaseStarted(SeederPhases.CreatingCollections, count));
        var ticker = new ProgressTicker(progress, SeederPhases.CreatingCollections, count);
        var collections = new List<Collection>(count);
        for (var i = 0; i < count; i++)
        {
            collections.Add(CollectionSeeder.Create(orgId, orgKey, $"Collection {i + 1}"));
            ticker.Tick();
        }
        ticker.Flush();

        var collectionIds = collections.Select(c => c.Id).ToList();
        context.Collections.AddRange(collections);
        registry.CollectionIds.AddRange(collectionIds);
        registry.CollectionTargetSizes.AddRange(sizes);
        registry.MaxCollectionsPerCipher = collectionShape.MaxCollectionsPerCipher ?? 1;

        // Hot-pair collections live in their hot group's department; the rest are spread at random
        var departmentCount = Math.Max(shape.Departments?.Count ?? 1, 1);
        var departments = new int[count];
        for (var i = 0; i < count; i++)
        {
            departments[i] = i < pinned.Count && pinned[i].Group >= 0 && pinned[i].Group < registry.HotGroupIds.Count
                ? Math.Max(registry.GroupDepartments.GetValueOrDefault(registry.HotGroupIds[pinned[i].Group]), 0)
                : random.Next(departmentCount);
        }
        registry.CollectionDepartments.AddRange(departments);
        var byDepartment = Enumerable.Range(0, departmentCount)
            .Select(d => Enumerable.Range(0, count).Where(i => departments[i] == d).ToArray())
            .ToArray();

        context.CollectionGroups.AddRange(BuildGroupGrants(registry, context.GroupUsers, collectionIds, sizes, pinned, byDepartment, grantShape, random));
        context.CollectionUsers.AddRange(BuildDirectGrants(registry, context.GroupUsers, collectionIds, sizes, byDepartment, directShape, random));

        progress?.Report(new PhaseCompleted(SeederPhases.CreatingCollections));
    }

    private List<CollectionGroup> BuildGroupGrants(
        EntityRegistry registry,
        List<GroupUser> groupUsers,
        List<Guid> collectionIds,
        int[] sizes,
        List<(int Group, int size)> pinned,
        int[][] byDepartment,
        SeedPresetShapeGroupGrants grantShape,
        Random random)
    {
        var grants = new List<CollectionGroup>();
        var seen = new HashSet<(Guid, Guid)>();

        bool Grant(int collectionIndex, Guid groupId)
        {
            if (!seen.Add((collectionIds[collectionIndex], groupId)))
            {
                return false;
            }
            grants.Add(CollectionGroupSeeder.Create(collectionIds[collectionIndex], groupId));
            return true;
        }

        foreach (var pair in grantShape.HotPairs ?? [])
        {
            if (pair.Group < 0 || pair.Group >= registry.HotGroupIds.Count)
            {
                throw new InvalidOperationException($"Hot pair references group {pair.Group}, but only {registry.HotGroupIds.Count} hot groups exist.");
            }

            foreach (var i in pair.Collections)
            {
                if (i < 0 || i >= pinned.Count)
                {
                    throw new InvalidOperationException($"Hot pair references hot collection {i}, but only {pinned.Count} exist.");
                }
                Grant(i, registry.HotGroupIds[pair.Group]);
            }
        }

        if (registry.EveryoneGroupId is { } everyoneId && grantShape.EveryoneGroupCollections is > 0)
        {
            var tiny = Enumerable.Range(0, sizes.Length).Where(i => sizes[i] is >= 1 and <= 2).ToArray();
            random.Shuffle(tiny);
            foreach (var i in tiny.Take(grantShape.EveryoneGroupCollections.Value))
            {
                Grant(i, everyoneId);
            }
        }

        var wideGroupIds = GrantWideGroups(registry, sizes, pinned.Count, grantShape.WideGroups, random, Grant);

        // Groups left without grants are the smallest ones (unused directory groups), not a random sample
        var grantedRate = grantShape.GrantedGroupRate ?? 1.0;
        var regular = registry.GroupMemberCounts
            .Where(kv => kv.Key != registry.EveryoneGroupId && kv.Value > 0 && !wideGroupIds.Contains(kv.Key)
                && !registry.HotGroupIds.Contains(kv.Key))
            .OrderByDescending(kv => kv.Value).ThenBy(_ => random.Next())
            .Select(kv => kv.Key)
            .ToArray();
        var candidates = regular.Take((int)Math.Round(regular.Length * grantedRate)).ToArray();
        var target = grantShape.Total ?? grants.Count;
        if (candidates.Length > 0)
        {
            var bias = grantShape.GroupSizeBias ?? 0;
            var appetite = grantShape.GroupAppetiteAlpha is { } appetiteAlpha
                ? SizeHistogram.ParetoWeights(candidates.Length, appetiteAlpha, random)
                : Enumerable.Repeat(1.0, candidates.Length).ToArray();
            var picker = new WeightedPicker(candidates
                .Select((g, i) => Math.Pow(registry.GroupMemberCounts[g], -bias) * appetite[i])
                .ToArray());
            var popularity = grantShape.CollectionPopularityAlpha is { } alpha
                ? SizeHistogram.ParetoWeights(collectionIds.Count, alpha, random)
                : Enumerable.Repeat(1.0, collectionIds.Count).ToArray();
            var sizeBias = grantShape.CollectionSizeBias ?? 0;
            for (var i = 0; i < popularity.Length; i++)
            {
                popularity[i] *= Math.Pow(sizes[i] + 1, sizeBias);
            }
            var globalPicker = new WeightedPicker(popularity);
            var departmentPickers = byDepartment
                .Select(ids => ids.Length > 0 ? new WeightedPicker(ids.Select(i => popularity[i]).ToArray()) : null)
                .ToArray();
            // Echo targets are sub-teams: peers sharing members that are no bigger than the source group
            var overlapping = OverlappingGroups(groupUsers, registry.EveryoneGroupId)
                .ToDictionary(kv => kv.Key, kv => kv.Value
                    .Where(h => registry.GroupMemberCounts[h] <= registry.GroupMemberCounts[kv.Key])
                    .ToArray());
            var echoRate = grantShape.EchoRate ?? 0;
            var randomGrants = new List<(int Collection, Guid Group)>();
            var randomGrantCounts = new Dictionary<Guid, int>();
            var maxPerGroup = grantShape.MaxGrantsPerGroup ?? int.MaxValue;
            var maxAttempts = target * 100;
            for (var attempt = 0; grants.Count < target && attempt < maxAttempts; attempt++)
            {
                if (randomGrants.Count > 0 && random.NextDouble() < echoRate)
                {
                    var (echoCollection, sourceGroup) = randomGrants[random.Next(randomGrants.Count)];
                    if (registry.GroupChildren.TryGetValue(sourceGroup, out var children) && children.Count > 0)
                    {
                        Grant(echoCollection, children[random.Next(children.Count)]);
                    }
                    else if (overlapping.TryGetValue(sourceGroup, out var peers) && peers.Length > 0)
                    {
                        Grant(echoCollection, peers[random.Next(peers.Length)]);
                    }
                    continue;
                }

                var groupId = candidates[picker.Pick(random)];
                if (randomGrantCounts.GetValueOrDefault(groupId) >= maxPerGroup)
                {
                    continue;
                }
                var department = registry.GroupDepartments.GetValueOrDefault(groupId, -1);
                var local = department >= 0 && departmentPickers[department] is not null && random.NextDouble() < registry.DepartmentLocality;
                var c = local
                    ? byDepartment[department][departmentPickers[department]!.Pick(random)]
                    : globalPicker.Pick(random);

                // A high-appetite group can exhaust its department; spill over org-wide instead of stalling
                if (!Grant(c, groupId) && !Grant(c = globalPicker.Pick(random), groupId))
                {
                    continue;
                }
                randomGrants.Add((c, groupId));
                randomGrantCounts[groupId] = randomGrantCounts.GetValueOrDefault(groupId) + 1;
            }
        }

        ApplyShuffledPermissions(grants, random,
            CreateCollectionsStep.ApplyGroupPermissions);
        return grants;
    }

    /// <summary>
    /// Grants a few tiny groups nested prefixes of one shared block of near-empty collections.
    /// Returns the chosen groups so the random grant pass leaves them alone.
    /// </summary>
    private static HashSet<Guid> GrantWideGroups(
        EntityRegistry registry,
        int[] sizes,
        int pinnedCount,
        SeedPresetShapeWideGroups? wide,
        Random random,
        Func<int, Guid, bool> grant)
    {
        var chosen = new HashSet<Guid>();
        if (wide is not { Count: > 0, BlockSize: > 0 })
        {
            return chosen;
        }

        var eligible = Enumerable.Range(pinnedCount, sizes.Length - pinnedCount)
            .Where(i => sizes[i] <= wide.BlockMaxItems)
            .ToArray();
        random.Shuffle(eligible);
        var block = eligible.Take(wide.BlockSize).ToArray();

        var groups = registry.GroupMemberCounts
            .Where(kv => kv.Key != registry.EveryoneGroupId && !registry.HotGroupIds.Contains(kv.Key)
                && kv.Value >= 1 && kv.Value <= wide.MaxMembers)
            .Select(kv => kv.Key)
            .ToArray();
        random.Shuffle(groups);

        foreach (var groupId in groups.Take(wide.Count))
        {
            // The first wide group gets the full MaxGrants so the widest reach is pinned; the rest are log-uniform
            var lo = Math.Log(Math.Max(wide.MinGrants, 1));
            var hi = Math.Log(Math.Max(Math.Max(wide.MaxGrants, wide.MinGrants), 1));
            var n = Math.Min(chosen.Count == 0 ? wide.MaxGrants : (int)Math.Exp(lo + random.NextDouble() * (hi - lo)), block.Length);
            chosen.Add(groupId);

            // Prefixes of one fixed order: a core is granted by every wide group, the tail by fewer
            foreach (var c in block.Take(n))
            {
                grant(c, groupId);
            }
        }

        return chosen;
    }

    /// <summary>
    /// For each group, the other groups (excluding everyone) that share at least one member.
    /// </summary>
    private static Dictionary<Guid, Guid[]> OverlappingGroups(List<GroupUser> groupUsers, Guid? everyoneGroupId)
    {
        var groupsByMember = groupUsers
            .Where(gu => gu.GroupId != everyoneGroupId)
            .GroupBy(gu => gu.OrganizationUserId)
            .Select(g => g.Select(gu => gu.GroupId).ToArray())
            .Where(groups => groups.Length > 1);

        var peers = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var groups in groupsByMember)
        {
            foreach (var g in groups)
            {
                if (!peers.TryGetValue(g, out var set))
                {
                    peers[g] = set = [];
                }
                set.UnionWith(groups.Where(h => h != g));
            }
        }

        return peers.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    private List<CollectionUser> BuildDirectGrants(
        EntityRegistry registry,
        List<GroupUser> groupUsers,
        List<Guid> collectionIds,
        int[] sizes,
        int[][] byDepartment,
        SeedPresetShapeDirectGrants directShape,
        Random random)
    {
        var members = registry.HardenedOrgUserIds;
        var grants = new List<CollectionUser>();
        var target = directShape.Total ?? 0;
        if (target == 0 || members.Count == 0)
        {
            return grants;
        }

        var bias = directShape.CollectionSizeBias ?? 0;
        var collectionPicker = new WeightedPicker(sizes.Select(s => Math.Pow(s + 1, bias)).ToArray());
        var departmentPickers = byDepartment
            .Select(ids => ids.Length > 0 ? new WeightedPicker(ids.Select(i => Math.Pow(sizes[i] + 1, bias)).ToArray()) : null)
            .ToArray();
        var memberWeights = SizeHistogram.ParetoWeights(members.Count, directShape.UserAffinityAlpha ?? 1.5, random);
        if (directShape.UngroupedBoost is { } boost)
        {
            var grouped = groupUsers.Where(gu => gu.GroupId != registry.EveryoneGroupId)
                .Select(gu => gu.OrganizationUserId).ToHashSet();
            for (var u = 0; u < members.Count; u++)
            {
                if (!grouped.Contains(members[u]))
                {
                    memberWeights[u] *= boost;
                }
            }
        }
        var memberPicker = new WeightedPicker(memberWeights);
        var seen = new HashSet<(int, int)>();
        var maxAttempts = target * 20;

        for (var attempt = 0; grants.Count < target && attempt < maxAttempts; attempt++)
        {
            var u = memberPicker.Pick(random);
            var department = registry.MemberDepartments.Count > u ? registry.MemberDepartments[u] : -1;
            var local = department >= 0 && departmentPickers[department] is not null && random.NextDouble() < registry.DepartmentLocality;
            var c = local
                ? byDepartment[department][departmentPickers[department]!.Pick(random)]
                : collectionPicker.Pick(random);
            if (seen.Add((c, u)))
            {
                grants.Add(CollectionUserSeeder.Create(collectionIds[c], members[u]));
            }
        }

        ApplyShuffledPermissions(grants, random,
            CreateCollectionsStep.ApplyUserPermissions);
        return grants;
    }

    // Distribution assigns permissions in contiguous index blocks; shuffle so they don't correlate with grant source
    private void ApplyShuffledPermissions<T>(List<T> grants, Random random, Action<List<T>, Distribution<PermissionWeight>> apply)
    {
        var shuffled = grants.ToArray();
        random.Shuffle(shuffled);
        apply(shuffled.ToList(), permissions);
    }

    /// <summary>
    /// O(log n) weighted index picker over a fixed weight vector.
    /// </summary>
    private sealed class WeightedPicker
    {
        private readonly double[] _cumulative;

        internal WeightedPicker(double[] weights)
        {
            _cumulative = new double[weights.Length];
            var sum = 0.0;
            for (var i = 0; i < weights.Length; i++)
            {
                sum += weights[i];
                _cumulative[i] = sum;
            }
        }

        internal int Pick(Random random)
        {
            var x = random.NextDouble() * _cumulative[^1];
            var index = Array.BinarySearch(_cumulative, x);
            return Math.Min(index < 0 ? ~index : index, _cumulative.Length - 1);
        }
    }
}
