using Bit.Core.AdminConsole.Entities;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates groups with overlapping membership: an optional "everyone" group, fixed-size hot groups,
/// and long-tailed remaining groups. Members get a Pareto affinity so a few belong to many groups
/// (up to <see cref="SeedPresetShapeGroups.MaxGroupsPerUser"/>) while most belong to one or two.
/// </summary>
/// <remarks>
/// Replaces <see cref="CreateGroupsStep"/> when a preset has an <c>accessShape</c> block. Real large orgs
/// put members in 2–17 groups; the overlap is what produces duplicate sync rows and most report rows.
/// With <see cref="SeedPresetShapeDepartments"/>, each member and group gets a home department and groups
/// draw mostly from their own department.
/// <para>
/// Presets are calibrated against the exact <see cref="Random"/> stream, so every draw must stay in its
/// current order: sizes, affinity, member departments, everyone group, hot groups, regular groups.
/// </para>
/// </remarks>
internal sealed class CreateShapedGroupsStep(int count, SeedPresetAccessShape accessShape, int seed) : IStep
{
    private readonly SeedPresetShapeGroups _shape = accessShape.Groups ?? new SeedPresetShapeGroups();

    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var progress = context.GetProgress();
        var random = new Random(seed);

        var everyoneRate = _shape.EveryoneGroupRate ?? 0;
        var hotSizes = _shape.HotGroupSizes ?? [];
        var regularSizes = DrawRegularSizes(RegularGroupCount(count, hotSizes.Count, everyoneRate > 0), random);

        progress?.Report(new PhaseStarted(SeederPhases.CreatingGroups, count));

        var build = new ShapedGroupBuild(context.Registry, orgId, _shape, accessShape.Departments, random);
        if (everyoneRate > 0)
        {
            build.AddEveryoneGroup(everyoneRate);
        }
        build.AddHotGroups(hotSizes, _shape.HotGroupParents, _shape.HotGroupOverlap ?? 0);
        build.AddRegularGroups(regularSizes, _shape.NestedGroupRate ?? 0);
        build.CommitTo(context);

        progress?.Report(new PhaseAdvanced(SeederPhases.CreatingGroups, count));
        progress?.Report(new PhaseCompleted(SeederPhases.CreatingGroups));
    }

    /// <summary>
    /// Weight of an out-of-department member relative to an in-department one, chosen so a group's expected
    /// in-department share equals <paramref name="locality"/> when affinities are equal:
    /// 1 / (1 + (departmentCount - 1) × weight) = locality. Locality 0 means no locality (weight 1).
    /// </summary>
    internal static double OutOfDepartmentWeight(double locality, int departmentCount) =>
        locality > 0 ? (1 - locality) / (locality * (departmentCount - 1)) : 1.0;

    /// <summary>
    /// Parent hot group index for hot group <paramref name="index"/>: the configured entry when there is one,
    /// otherwise -1 for the first hot group and 0 (the first hot group) for every later one.
    /// </summary>
    internal static int ResolveHotParent(IReadOnlyList<int>? parents, int index) =>
        parents is not null && index < parents.Count ? parents[index] : (index == 0 ? -1 : 0);

    private static int RegularGroupCount(int count, int hotCount, bool hasEveryone)
    {
        var regularCount = count - hotCount - (hasEveryone ? 1 : 0);
        if (regularCount < 0)
        {
            throw new InvalidOperationException(
                $"groups.count ({count}) is smaller than the everyone group plus {hotCount} hot groups.");
        }

        return regularCount;
    }

    private int[] DrawRegularSizes(int regularCount, Random random) =>
        _shape.Sizes is { Count: > 0 }
            ? new SizeHistogram(_shape.Sizes.Select(b => (b.Min, b.Max, b.Weight))).Draw(regularCount, random)
            : new int[regularCount];

    /// <summary>
    /// Mutable state for one <see cref="Execute"/> run: per-member group counts, the groups and memberships
    /// built so far, and the member affinities and departments every group draws from.
    /// </summary>
    private sealed class ShapedGroupBuild
    {
        private readonly EntityRegistry _registry;
        private readonly Guid _orgId;
        private readonly List<Guid> _members;
        private readonly Random _random;
        private readonly int _maxGroupsPerUser;
        private readonly int _departmentCount;
        private readonly double[] _affinity;
        private readonly int[] _memberDepartments;
        private readonly double _leak;
        private readonly int[] _membershipCounts;
        private readonly List<Group> _groups = [];
        private readonly List<GroupUser> _groupUsers = [];

        internal ShapedGroupBuild(
            EntityRegistry registry,
            Guid orgId,
            SeedPresetShapeGroups shape,
            SeedPresetShapeDepartments? departments,
            Random random)
        {
            _registry = registry;
            _orgId = orgId;
            _members = registry.HardenedOrgUserIds;
            _random = random;
            _membershipCounts = new int[_members.Count];
            _maxGroupsPerUser = shape.MaxGroupsPerUser ?? int.MaxValue;
            _affinity = SizeHistogram.ParetoWeights(_members.Count, shape.UserAffinityAlpha ?? 1.5, random);

            _departmentCount = Math.Max(departments?.Count ?? 1, 1);
            var locality = _departmentCount > 1 ? departments?.Locality ?? 0 : 0;
            _memberDepartments = Enumerable.Range(0, _members.Count).Select(_ => random.Next(_departmentCount)).ToArray();
            registry.MemberDepartments.AddRange(_memberDepartments);
            registry.DepartmentLocality = locality;
            _leak = OutOfDepartmentWeight(locality, _departmentCount);
        }

        internal void AddEveryoneGroup(double rate)
        {
            var uniform = Enumerable.Repeat(1.0, _members.Count).ToArray();
            var everyone = AddGroup("Everyone", (int)Math.Round(_members.Count * rate), uniform);
            _registry.EveryoneGroupId = everyone.Id;
        }

        internal void AddHotGroups(List<int> sizes, IReadOnlyList<int>? parents, double overlap)
        {
            var hotMembers = new List<List<int>>();
            // Drawn even when there are no hot groups, to keep the calibrated stream
            var hotDepartment = _random.Next(_departmentCount);
            for (var h = 0; h < sizes.Count; h++)
            {
                var seedMembers = SeedFromParent(hotMembers, ResolveHotParent(parents, h), sizes[h], overlap);

                // Overlapping hot groups share a department; independent ones get their own
                var department = seedMembers is { Count: > 0 } || h == 0 ? hotDepartment : _random.Next(_departmentCount);
                var hot = AddGroup($"Hot Group {h + 1}", sizes[h], DepartmentWeights(department), department, seedMembers);
                ShapeTargets.EnsureReached($"members in Hot Group {h + 1}", hot.Members.Count, sizes[h]);
                hotMembers.Add(hot.Members);
                _registry.HotGroupIds.Add(hot.Id);
            }
        }

        internal void AddRegularGroups(int[] sizes, double nestedRate)
        {
            // Fill big groups first so the per-member cap only bites on the long tail, and so nested children
            // can find a bigger parent
            var created = new List<BuiltGroup>();
            var order = Enumerable.Range(0, sizes.Length).OrderByDescending(i => sizes[i]).ToArray();
            foreach (var i in order)
            {
                var size = sizes[i];
                var parents = size > 0 && _random.NextDouble() < nestedRate
                    ? created.Where(p => p.Members.Count > size).ToList()
                    : [];

                created.Add(parents.Count > 0
                    ? AddNestedGroup($"Group {i + 1}", size, parents)
                    : AddIndependentGroup($"Group {i + 1}", size));
            }
        }

        internal void CommitTo(SeederContext context)
        {
            context.Groups.AddRange(_groups);
            context.GroupUsers.AddRange(_groupUsers);
        }

        private List<int>? SeedFromParent(List<List<int>> hotMembers, int parent, int size, double overlap)
        {
            if (parent < 0 || parent >= hotMembers.Count)
            {
                return null;
            }

            return hotMembers[parent].OrderBy(_ => _random.Next())
                .Take((int)Math.Round(size * overlap))
                .Where(u => _membershipCounts[u] < _maxGroupsPerUser)
                .ToList();
        }

        private BuiltGroup AddNestedGroup(string name, int size, List<BuiltGroup> parents)
        {
            var parent = parents[_random.Next(parents.Count)];
            var subset = parent.Members.OrderBy(_ => _random.Next())
                .Where(u => _membershipCounts[u] < _maxGroupsPerUser)
                .Take(size)
                .ToList();
            var child = AddGroup(name, size, DepartmentWeights(parent.Department), parent.Department, subset);

            if (!_registry.GroupChildren.TryGetValue(parent.Id, out var children))
            {
                _registry.GroupChildren[parent.Id] = children = [];
            }
            children.Add(child.Id);
            return child;
        }

        private BuiltGroup AddIndependentGroup(string name, int size)
        {
            var department = _random.Next(_departmentCount);
            return AddGroup(name, size, DepartmentWeights(department), department);
        }

        private double[] DepartmentWeights(int department) => _affinity
            .Select((w, u) => _memberDepartments[u] == department ? w : w * _leak)
            .ToArray();

        /// <summary>
        /// Creates one group with <paramref name="seedMembers"/> (up to <paramref name="size"/>) plus a weighted
        /// draw of the rest from members not already in it and still under the per-member cap.
        /// </summary>
        private BuiltGroup AddGroup(string name, int size, double[] weights, int department = -1, List<int>? seedMembers = null)
        {
            var group = GroupSeeder.Create(_orgId, name);
            _groups.Add(group);
            _registry.GroupDepartments[group.Id] = department;

            var picked = seedMembers?.Take(size).ToList() ?? [];
            var already = picked.ToHashSet();
            picked.AddRange(SizeHistogram.SampleWithoutReplacement(
                weights, size - picked.Count, _random, u => !already.Contains(u) && _membershipCounts[u] < _maxGroupsPerUser));
            foreach (var u in picked)
            {
                _groupUsers.Add(GroupUserSeeder.Create(group.Id, _members[u]));
                _membershipCounts[u]++;
            }

            _registry.GroupIds.Add(group.Id);
            _registry.GroupMemberCounts[group.Id] = picked.Count;
            return new BuiltGroup(group.Id, department, picked);
        }
    }

    /// <summary>
    /// A created group, its department (-1 for the everyone group) and its member indices.
    /// </summary>
    private sealed record BuiltGroup(Guid Id, int Department, List<int> Members);
}
