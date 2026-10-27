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
/// <para>
/// Presets are calibrated against the exact <see cref="Random"/> streams, so every draw must stay in its
/// current order: sizes, collection departments, everyone group, wide groups, random group grants, group
/// permissions, direct grants, direct permissions. Inactive member access runs on its own stream.
/// </para>
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

        var collectionShape = shape.Collections ?? new SeedPresetShapeCollections();
        var grantShape = shape.GroupGrants ?? new SeedPresetShapeGroupGrants();
        var hotCollections = grantShape.HotCollections ?? [];
        var hotCollectionGroups = HotCollectionGroups(hotCollections.Count, grantShape.HotPairs ?? []);
        var sizes = DrawSizes(hotCollections, collectionShape, random);

        progress?.Report(new PhaseStarted(SeederPhases.CreatingCollections, count));
        var collectionIds = AddCollections(context, orgId, orgKey, progress, sizes, collectionShape.MaxCollectionsPerCipher ?? 1);

        var build = new ShapedCollectionBuild(
            context.Registry, context.GroupUsers, collectionIds, sizes, hotCollectionGroups, shape.Departments, permissions, random);
        build.GrantHotPairs(grantShape.HotPairs ?? []);
        build.GrantEveryoneGroup(grantShape.EveryoneGroupCollections);
        build.GrantWideGroups(grantShape.WideGroups);
        build.GrantRandomly(grantShape);
        build.ApplyGroupPermissions();
        build.GrantDirectly(shape.DirectGrants ?? new SeedPresetShapeDirectGrants());
        // Own random stream, so confirmed members' shape is the same with or without inactive access
        build.AddInactiveMemberAccess(shape.InactiveAccessRate ?? 0, new Random(seed + 3));
        build.CommitTo(context);

        progress?.Report(new PhaseCompleted(SeederPhases.CreatingCollections));
    }

    /// <summary>
    /// Hot group index per hot collection: the group of the first hot pair that lists it, or -1 when no pair does.
    /// A hot collection lives in that group's department.
    /// </summary>
    internal static int[] HotCollectionGroups(int hotCollectionCount, IReadOnlyList<SeedPresetShapeHotPair> hotPairs) =>
        Enumerable.Range(0, hotCollectionCount)
            .Select(i => hotPairs.FirstOrDefault(p => p.Collections.Contains(i))?.Group ?? -1)
            .ToArray();

    /// <summary>
    /// Grants for a wide group after the first, for a uniform draw <paramref name="u"/> in [0, 1): log-uniform in
    /// [MinGrants, MaxGrants), floored, so never below MinGrants and below MaxGrants unless the two are equal.
    /// MinGrants is raised to at least 1 and MaxGrants to at least MinGrants.
    /// </summary>
    internal static int LogUniformGrantCount(int minGrants, int maxGrants, double u)
    {
        var low = Math.Max(minGrants, 1);
        var lo = Math.Log(low);
        var hi = Math.Log(Math.Max(Math.Max(maxGrants, minGrants), 1));
        // Exp(Log(n)) can land just under n, so the floor alone could give low - 1 (also when low equals high)
        return Math.Max((int)Math.Exp(lo + u * (hi - lo)), low);
    }

    /// <summary>
    /// For each group, the other groups (excluding everyone) that share at least one member.
    /// </summary>
    internal static Dictionary<Guid, Guid[]> OverlappingGroups(List<GroupUser> groupUsers, Guid? everyoneGroupId)
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

    /// <summary>
    /// Target sizes: the pinned hot collections first, then histogram draws scaled to the rest of
    /// <see cref="SeedPresetShapeCollections.TotalAssignments"/>.
    /// </summary>
    private int[] DrawSizes(List<int> hotCollections, SeedPresetShapeCollections collectionShape, Random random)
    {
        var drawnCount = count - hotCollections.Count;
        if (drawnCount < 0)
        {
            throw new InvalidOperationException($"collections.count ({count}) is smaller than the {hotCollections.Count} hot-pair collections.");
        }

        var buckets = collectionShape.Sizes ?? [new SeedPresetSizeBucket { Min = 1, Max = 1, Weight = 1 }];
        var drawn = new SizeHistogram(buckets.Select(b => (b.Min, b.Max, b.Weight))).Draw(drawnCount, random);
        if (collectionShape.TotalAssignments is { } totalAssignments)
        {
            var drawnTarget = totalAssignments - hotCollections.Sum();
            SizeHistogram.ScaleTo(drawn, drawnTarget, buckets.Max(b => b.Max));
            ShapeTargets.EnsureReached("collection slots", drawn.Sum(s => (long)s), drawnTarget);
        }

        return hotCollections.Concat(drawn).ToArray();
    }

    private List<Guid> AddCollections(
        SeederContext context,
        Guid orgId,
        string orgKey,
        IProgress<SeederProgressEvent>? progress,
        int[] sizes,
        int maxCollectionsPerCipher)
    {
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
        context.Registry.CollectionIds.AddRange(collectionIds);
        context.Registry.CollectionTargetSizes.AddRange(sizes);
        context.Registry.MaxCollectionsPerCipher = maxCollectionsPerCipher;
        return collectionIds;
    }

    /// <summary>
    /// Mutable state for one <see cref="Execute"/> run: the collections and their departments, and the
    /// group grants, direct grants and inactive-member rows built so far.
    /// </summary>
    private sealed class ShapedCollectionBuild
    {
        private readonly EntityRegistry _registry;
        private readonly List<GroupUser> _groupUsers;
        private readonly List<Guid> _collectionIds;
        private readonly int[] _sizes;
        private readonly int[] _hotCollectionGroups;
        private readonly Distribution<PermissionWeight> _permissions;
        private readonly Random _random;
        private readonly int[][] _byDepartment;
        private readonly List<CollectionGroup> _groupGrants = [];
        private readonly HashSet<(Guid, Guid)> _seen = [];
        private readonly List<CollectionUser> _directGrants = [];
        private readonly List<GroupUser> _inactiveGroupUsers = [];
        private readonly List<CollectionUser> _inactiveCollectionUsers = [];
        private readonly HashSet<Guid> _wideGroupIds = [];

        internal ShapedCollectionBuild(
            EntityRegistry registry,
            List<GroupUser> groupUsers,
            List<Guid> collectionIds,
            int[] sizes,
            int[] hotCollectionGroups,
            SeedPresetShapeDepartments? departments,
            Distribution<PermissionWeight> permissions,
            Random random)
        {
            _registry = registry;
            _groupUsers = groupUsers;
            _collectionIds = collectionIds;
            _sizes = sizes;
            _hotCollectionGroups = hotCollectionGroups;
            _permissions = permissions;
            _random = random;

            var departmentCount = Math.Max(departments?.Count ?? 1, 1);
            var collectionDepartments = Enumerable.Range(0, collectionIds.Count)
                .Select(i => HomeDepartment(i, departmentCount))
                .ToArray();
            registry.CollectionDepartments.AddRange(collectionDepartments);
            _byDepartment = Enumerable.Range(0, departmentCount)
                .Select(d => Enumerable.Range(0, collectionIds.Count).Where(i => collectionDepartments[i] == d).ToArray())
                .ToArray();
        }

        internal void GrantHotPairs(List<SeedPresetShapeHotPair> hotPairs)
        {
            foreach (var pair in hotPairs)
            {
                if (pair.Group < 0 || pair.Group >= _registry.HotGroupIds.Count)
                {
                    throw new InvalidOperationException($"Hot pair references group {pair.Group}, but only {_registry.HotGroupIds.Count} hot groups exist.");
                }

                foreach (var i in pair.Collections)
                {
                    if (i < 0 || i >= _hotCollectionGroups.Length)
                    {
                        throw new InvalidOperationException($"Hot pair references hot collection {i}, but only {_hotCollectionGroups.Length} exist.");
                    }
                    Grant(i, _registry.HotGroupIds[pair.Group]);
                }
            }
        }

        internal void GrantEveryoneGroup(int? collectionCount)
        {
            if (_registry.EveryoneGroupId is not { } everyoneId || collectionCount is not > 0)
            {
                return;
            }

            var tiny = Enumerable.Range(0, _sizes.Length).Where(i => _sizes[i] is >= 1 and <= 2).ToArray();
            _random.Shuffle(tiny);
            foreach (var i in tiny.Take(collectionCount.Value))
            {
                Grant(i, everyoneId);
            }
        }

        /// <summary>
        /// Grants a few tiny groups nested prefixes of one shared block of near-empty collections, and
        /// remembers them so the random pass never picks them. Echo grants can still reach them.
        /// </summary>
        internal void GrantWideGroups(SeedPresetShapeWideGroups? wide)
        {
            if (wide is not { Count: > 0, BlockSize: > 0 })
            {
                return;
            }

            var pinnedCount = _hotCollectionGroups.Length;
            var eligible = Enumerable.Range(pinnedCount, _sizes.Length - pinnedCount)
                .Where(i => _sizes[i] <= wide.BlockMaxItems)
                .ToArray();
            _random.Shuffle(eligible);
            var block = eligible.Take(wide.BlockSize).ToArray();

            var groups = _registry.GroupMemberCounts
                .Where(kv => kv.Key != _registry.EveryoneGroupId && !_registry.HotGroupIds.Contains(kv.Key)
                    && kv.Value >= 1 && kv.Value <= wide.MaxMembers)
                .Select(kv => kv.Key)
                .ToArray();
            _random.Shuffle(groups);

            foreach (var groupId in groups.Take(wide.Count))
            {
                // The first wide group gets the full MaxGrants so the widest reach is pinned; the rest are log-uniform
                var grants = _wideGroupIds.Count == 0
                    ? wide.MaxGrants
                    : LogUniformGrantCount(wide.MinGrants, wide.MaxGrants, _random.NextDouble());
                _wideGroupIds.Add(groupId);

                // Prefixes of one fixed order: a core is granted by every wide group, the tail by fewer
                foreach (var c in block.Take(Math.Min(grants, block.Length)))
                {
                    Grant(c, groupId);
                }
            }
        }

        /// <summary>
        /// Fills <see cref="SeedPresetShapeGroupGrants.Total"/> with weighted random grants to the regular groups,
        /// some of them echoed to a nested or overlapping group so shared members get a second path.
        /// </summary>
        internal void GrantRandomly(SeedPresetShapeGroupGrants grantShape)
        {
            var candidates = RandomGrantCandidates(grantShape.GrantedGroupRate ?? 1.0);
            var target = grantShape.Total ?? _groupGrants.Count;
            if (candidates.Length > 0)
            {
                FillWithRandomGrants(candidates, target, grantShape);
            }

            if (grantShape.Total is { } total)
            {
                ShapeTargets.EnsureReached("group grants", _groupGrants.Count, total);
            }
        }

        internal void ApplyGroupPermissions() =>
            ApplyShuffledPermissions(_groupGrants, CreateCollectionsStep.ApplyGroupPermissions);

        internal void GrantDirectly(SeedPresetShapeDirectGrants directShape)
        {
            var members = _registry.HardenedOrgUserIds;
            var target = directShape.Total ?? 0;
            if (target == 0 || members.Count == 0)
            {
                return;
            }

            var bias = directShape.CollectionSizeBias ?? 0;
            var collectionPicker = new WeightedPicker(_sizes.Select(s => Math.Pow(s + 1, bias)).ToArray());
            var departmentPickers = DepartmentPickers(i => Math.Pow(_sizes[i] + 1, bias));
            var memberPicker = new WeightedPicker(DirectGrantMemberWeights(directShape.UserAffinityAlpha ?? 1.5, directShape.UngroupedBoost));
            var seen = new HashSet<(int, int)>();
            var maxAttempts = target * 20;

            for (var attempt = 0; _directGrants.Count < target && attempt < maxAttempts; attempt++)
            {
                var u = memberPicker.Pick(_random);
                var department = _registry.MemberDepartments.Count > u ? _registry.MemberDepartments[u] : -1;
                var c = PickCollection(department, departmentPickers, collectionPicker);
                if (seen.Add((c, u)))
                {
                    _directGrants.Add(CollectionUserSeeder.Create(_collectionIds[c], members[u]));
                }
            }

            ShapeTargets.EnsureReached("direct grants", _directGrants.Count, target);

            ApplyShuffledPermissions(_directGrants, CreateCollectionsStep.ApplyUserPermissions);
        }

        /// <summary>
        /// Gives <paramref name="rate"/> of invited, accepted and revoked members the group memberships (except
        /// the everyone group) and direct grants of a random confirmed member. Real orgs leave revoked members in
        /// their groups and collections, and Access Intelligence maps them like anyone else.
        /// </summary>
        internal void AddInactiveMemberAccess(double rate, Random random)
        {
            var confirmed = _registry.HardenedOrgUserIds;
            if (rate <= 0 || confirmed.Count == 0)
            {
                return;
            }

            var groupsByMember = _groupUsers
                .Where(gu => gu.GroupId != _registry.EveryoneGroupId)
                .ToLookup(gu => gu.OrganizationUserId, gu => gu.GroupId);
            var grantsByMember = _directGrants.ToLookup(cu => cu.OrganizationUserId);

            foreach (var member in _registry.InactiveOrgUserIds)
            {
                if (random.NextDouble() >= rate)
                {
                    continue;
                }

                var twin = confirmed[random.Next(confirmed.Count)];
                _inactiveGroupUsers.AddRange(groupsByMember[twin].Select(groupId => GroupUserSeeder.Create(groupId, member)));
                _inactiveCollectionUsers.AddRange(grantsByMember[twin].Select(cu =>
                    CollectionUserSeeder.Create(cu.CollectionId, member, cu.ReadOnly, cu.HidePasswords, cu.Manage)));
            }
        }

        internal void CommitTo(SeederContext context)
        {
            context.CollectionGroups.AddRange(_groupGrants);
            context.CollectionUsers.AddRange(_directGrants);
            context.GroupUsers.AddRange(_inactiveGroupUsers);
            context.CollectionUsers.AddRange(_inactiveCollectionUsers);
        }

        // Hot-pair collections live in their hot group's department; the rest are spread at random
        private int HomeDepartment(int collectionIndex, int departmentCount)
        {
            var hotGroup = collectionIndex < _hotCollectionGroups.Length ? _hotCollectionGroups[collectionIndex] : -1;
            return hotGroup >= 0 && hotGroup < _registry.HotGroupIds.Count
                ? Math.Max(_registry.GroupDepartments.GetValueOrDefault(_registry.HotGroupIds[hotGroup]), 0)
                : _random.Next(departmentCount);
        }

        private bool Grant(int collectionIndex, Guid groupId)
        {
            if (!_seen.Add((_collectionIds[collectionIndex], groupId)))
            {
                return false;
            }
            _groupGrants.Add(CollectionGroupSeeder.Create(_collectionIds[collectionIndex], groupId));
            return true;
        }

        // Groups left without grants are the smallest ones (unused directory groups), not a random sample
        private Guid[] RandomGrantCandidates(double grantedRate)
        {
            var regular = _registry.GroupMemberCounts
                .Where(kv => kv.Key != _registry.EveryoneGroupId && kv.Value > 0 && !_wideGroupIds.Contains(kv.Key)
                    && !_registry.HotGroupIds.Contains(kv.Key))
                .OrderByDescending(kv => kv.Value).ThenBy(_ => _random.Next())
                .Select(kv => kv.Key)
                .ToArray();
            return regular.Take((int)Math.Round(regular.Length * grantedRate)).ToArray();
        }

        private void FillWithRandomGrants(Guid[] candidates, int target, SeedPresetShapeGroupGrants grantShape)
        {
            var groupPicker = GroupPicker(candidates, grantShape.GroupSizeBias ?? 0, grantShape.GroupAppetiteAlpha);
            var popularity = CollectionPopularity(grantShape.CollectionPopularityAlpha, grantShape.CollectionSizeBias ?? 0);
            var globalPicker = new WeightedPicker(popularity);
            var departmentPickers = DepartmentPickers(i => popularity[i]);
            var echoTargets = EchoTargets();
            var echoRate = grantShape.EchoRate ?? 0;
            var maxPerGroup = grantShape.MaxGrantsPerGroup ?? int.MaxValue;
            var randomGrants = new List<(int Collection, Guid Group)>();
            var randomGrantCounts = new Dictionary<Guid, int>();
            var maxAttempts = target * 100;

            for (var attempt = 0; _groupGrants.Count < target && attempt < maxAttempts; attempt++)
            {
                if (randomGrants.Count > 0 && _random.NextDouble() < echoRate)
                {
                    Echo(randomGrants, echoTargets);
                    continue;
                }

                var groupId = candidates[groupPicker.Pick(_random)];
                if (randomGrantCounts.GetValueOrDefault(groupId) >= maxPerGroup)
                {
                    continue;
                }

                if (GrantNearHome(groupId, departmentPickers, globalPicker) is { } collection)
                {
                    randomGrants.Add((collection, groupId));
                    randomGrantCounts[groupId] = randomGrantCounts.GetValueOrDefault(groupId) + 1;
                }
            }
        }

        private WeightedPicker GroupPicker(Guid[] candidates, double bias, double? appetiteAlpha)
        {
            var appetite = appetiteAlpha is { } alpha
                ? SizeHistogram.ParetoWeights(candidates.Length, alpha, _random)
                : Enumerable.Repeat(1.0, candidates.Length).ToArray();
            return new WeightedPicker(candidates
                .Select((g, i) => Math.Pow(_registry.GroupMemberCounts[g], -bias) * appetite[i])
                .ToArray());
        }

        private double[] CollectionPopularity(double? popularityAlpha, double sizeBias)
        {
            var popularity = popularityAlpha is { } alpha
                ? SizeHistogram.ParetoWeights(_collectionIds.Count, alpha, _random)
                : Enumerable.Repeat(1.0, _collectionIds.Count).ToArray();
            for (var i = 0; i < popularity.Length; i++)
            {
                popularity[i] *= Math.Pow(_sizes[i] + 1, sizeBias);
            }
            return popularity;
        }

        private WeightedPicker?[] DepartmentPickers(Func<int, double> weight) => _byDepartment
            .Select(ids => ids.Length > 0 ? new WeightedPicker(ids.Select(weight).ToArray()) : null)
            .ToArray();

        // Echo targets are sub-teams: peers sharing members that are no bigger than the source group
        private Dictionary<Guid, Guid[]> EchoTargets() => OverlappingGroups(_groupUsers, _registry.EveryoneGroupId)
            .ToDictionary(kv => kv.Key, kv => kv.Value
                .Where(h => _registry.GroupMemberCounts[h] <= _registry.GroupMemberCounts[kv.Key])
                .ToArray());

        /// <summary>
        /// Copies a random earlier random grant to a child of its group, or else to an overlapping peer.
        /// </summary>
        private void Echo(List<(int Collection, Guid Group)> randomGrants, Dictionary<Guid, Guid[]> echoTargets)
        {
            var (collection, sourceGroup) = randomGrants[_random.Next(randomGrants.Count)];
            if (_registry.GroupChildren.TryGetValue(sourceGroup, out var children) && children.Count > 0)
            {
                Grant(collection, children[_random.Next(children.Count)]);
            }
            else if (echoTargets.TryGetValue(sourceGroup, out var peers) && peers.Length > 0)
            {
                Grant(collection, peers[_random.Next(peers.Length)]);
            }
        }

        /// <summary>
        /// Grants the group one collection, picked in its department with probability
        /// <see cref="EntityRegistry.DepartmentLocality"/>. Returns the granted collection, or null when both the
        /// pick and an org-wide retry were already granted.
        /// </summary>
        private int? GrantNearHome(Guid groupId, WeightedPicker?[] departmentPickers, WeightedPicker globalPicker)
        {
            var department = _registry.GroupDepartments.GetValueOrDefault(groupId, -1);
            var collection = PickCollection(department, departmentPickers, globalPicker);
            if (Grant(collection, groupId))
            {
                return collection;
            }

            // A high-appetite group can exhaust its department; spill over org-wide instead of stalling
            collection = globalPicker.Pick(_random);
            return Grant(collection, groupId) ? collection : null;
        }

        private int PickCollection(int department, WeightedPicker?[] departmentPickers, WeightedPicker globalPicker)
        {
            if (department >= 0 && departmentPickers[department] is { } local && _random.NextDouble() < _registry.DepartmentLocality)
            {
                return _byDepartment[department][local.Pick(_random)];
            }
            return globalPicker.Pick(_random);
        }

        private double[] DirectGrantMemberWeights(double alpha, double? ungroupedBoost)
        {
            var members = _registry.HardenedOrgUserIds;
            var weights = SizeHistogram.ParetoWeights(members.Count, alpha, _random);
            if (ungroupedBoost is not { } boost)
            {
                return weights;
            }

            var grouped = _groupUsers.Where(gu => gu.GroupId != _registry.EveryoneGroupId)
                .Select(gu => gu.OrganizationUserId).ToHashSet();
            for (var u = 0; u < members.Count; u++)
            {
                if (!grouped.Contains(members[u]))
                {
                    weights[u] *= boost;
                }
            }
            return weights;
        }

        // Distribution assigns permissions in contiguous index blocks; shuffle so they don't correlate with grant source
        private void ApplyShuffledPermissions<T>(List<T> grants, Action<List<T>, Distribution<PermissionWeight>> apply)
        {
            var shuffled = grants.ToArray();
            _random.Shuffle(shuffled);
            apply(shuffled.ToList(), _permissions);
        }
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
