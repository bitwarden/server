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
/// </remarks>
internal sealed class CreateShapedGroupsStep(int count, SeedPresetAccessShape accessShape, int seed) : IStep
{
    private readonly SeedPresetShapeGroups _shape = accessShape.Groups ?? new SeedPresetShapeGroups();

    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();
        var members = context.Registry.HardenedOrgUserIds;
        var progress = context.GetProgress();
        var random = new Random(seed);

        var everyoneRate = _shape.EveryoneGroupRate ?? 0;
        var hotSizes = _shape.HotGroupSizes ?? [];
        var hasEveryone = everyoneRate > 0;
        var regularCount = count - hotSizes.Count - (hasEveryone ? 1 : 0);
        if (regularCount < 0)
        {
            throw new InvalidOperationException(
                $"groups.count ({count}) is smaller than the everyone group plus {hotSizes.Count} hot groups.");
        }

        var regularSizes = _shape.Sizes is { Count: > 0 }
            ? new SizeHistogram(_shape.Sizes.Select(b => (b.Min, b.Max, b.Weight))).Draw(regularCount, random)
            : new int[regularCount];

        progress?.Report(new PhaseStarted(SeederPhases.CreatingGroups, count));

        var groups = new List<Group>(count);
        var groupUsers = new List<GroupUser>();
        var membershipCounts = new int[members.Count];
        var maxGroupsPerUser = _shape.MaxGroupsPerUser ?? int.MaxValue;
        var affinity = SizeHistogram.ParetoWeights(members.Count, _shape.UserAffinityAlpha ?? 1.5, random);
        var uniform = Enumerable.Repeat(1.0, members.Count).ToArray();
        Group? lastGroup = null;

        var departmentCount = Math.Max(accessShape.Departments?.Count ?? 1, 1);
        var locality = departmentCount > 1 ? accessShape.Departments?.Locality ?? 0 : 0;
        var memberDepartments = Enumerable.Range(0, members.Count).Select(_ => random.Next(departmentCount)).ToArray();
        context.Registry.MemberDepartments.AddRange(memberDepartments);
        context.Registry.DepartmentLocality = locality;

        // Out-of-department weight chosen so a group's expected in-department share equals the locality
        var leak = locality > 0 ? (1 - locality) / (locality * (departmentCount - 1)) : 1.0;

        double[] DepartmentWeights(int department) => affinity
            .Select((w, u) => memberDepartments[u] == department ? w : w * leak)
            .ToArray();

        List<int> AddGroup(string name, int size, double[] weights, int department = -1, List<int>? seedMembers = null)
        {
            var group = GroupSeeder.Create(orgId, name);
            groups.Add(group);
            context.Registry.GroupDepartments[group.Id] = department;

            var picked = seedMembers?.Take(size).ToList() ?? [];
            var already = picked.ToHashSet();
            picked.AddRange(SizeHistogram.SampleWithoutReplacement(
                weights, size - picked.Count, random, u => !already.Contains(u) && membershipCounts[u] < maxGroupsPerUser));
            foreach (var u in picked)
            {
                groupUsers.Add(GroupUserSeeder.Create(group.Id, members[u]));
                membershipCounts[u]++;
            }

            context.Registry.GroupIds.Add(group.Id);
            context.Registry.GroupMemberCounts[group.Id] = picked.Count;
            lastGroup = group;
            return picked;
        }

        if (hasEveryone)
        {
            AddGroup("Everyone", (int)Math.Round(members.Count * everyoneRate), uniform);
            context.Registry.EveryoneGroupId = lastGroup!.Id;
        }

        var hotMembers = new List<List<int>>();
        var hotDepartment = random.Next(departmentCount);
        for (var h = 0; h < hotSizes.Count; h++)
        {
            var parent = _shape.HotGroupParents is { } parents && h < parents.Count ? parents[h] : (h == 0 ? -1 : 0);
            List<int>? seedMembers = null;
            if (parent >= 0 && parent < hotMembers.Count)
            {
                seedMembers = hotMembers[parent].OrderBy(_ => random.Next())
                    .Take((int)Math.Round(hotSizes[h] * (_shape.HotGroupOverlap ?? 0)))
                    .Where(u => membershipCounts[u] < maxGroupsPerUser)
                    .ToList();
            }

            // Overlapping hot groups share a department; independent ones get their own
            var department = seedMembers is { Count: > 0 } || h == 0 ? hotDepartment : random.Next(departmentCount);
            hotMembers.Add(AddGroup($"Hot Group {h + 1}", hotSizes[h], DepartmentWeights(department), department, seedMembers));
            context.Registry.HotGroupIds.Add(lastGroup!.Id);
        }

        // Fill big groups first so the per-member cap only bites on the long tail, and so nested children
        // can find a bigger parent
        var nestedRate = _shape.NestedGroupRate ?? 0;
        var created = new List<(Guid Id, int Department, List<int> Members)>();
        var order = Enumerable.Range(0, regularCount).OrderByDescending(i => regularSizes[i]).ToArray();
        foreach (var i in order)
        {
            var size = regularSizes[i];
            var parents = size > 0 && random.NextDouble() < nestedRate
                ? created.Where(p => p.Members.Count > size).ToList()
                : [];

            if (parents.Count > 0)
            {
                var parent = parents[random.Next(parents.Count)];
                var subset = parent.Members.OrderBy(_ => random.Next())
                    .Where(u => membershipCounts[u] < maxGroupsPerUser)
                    .Take(size)
                    .ToList();
                var picked = AddGroup($"Group {i + 1}", size, DepartmentWeights(parent.Department), parent.Department, subset);
                if (!context.Registry.GroupChildren.TryGetValue(parent.Id, out var children))
                {
                    context.Registry.GroupChildren[parent.Id] = children = [];
                }
                children.Add(lastGroup!.Id);
                created.Add((lastGroup.Id, parent.Department, picked));
            }
            else
            {
                var department = random.Next(departmentCount);
                var picked = AddGroup($"Group {i + 1}", size, DepartmentWeights(department), department);
                created.Add((lastGroup!.Id, department, picked));
            }
        }

        context.Groups.AddRange(groups);
        context.GroupUsers.AddRange(groupUsers);

        progress?.Report(new PhaseAdvanced(SeederPhases.CreatingGroups, count));
        progress?.Report(new PhaseCompleted(SeederPhases.CreatingGroups));
    }
}
