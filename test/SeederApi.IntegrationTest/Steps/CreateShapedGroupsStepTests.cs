using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class CreateShapedGroupsStepTests
{
    [Fact]
    public void Execute_EveryoneGroupRate_GivesEveryoneGroupRoundedShareOfMembers()
    {
        var context = ContextWithMembers(60);

        Run(context, 1, new SeedPresetShapeGroups { EveryoneGroupRate = 0.66 });

        // 60 × 0.66 = 39.6
        Assert.Equal(40, MembersOf(context, "Everyone").Count);
    }

    [Fact]
    public void Execute_EveryoneGroupRateZero_CreatesNoEveryoneGroup()
    {
        var context = ContextWithMembers(60);

        Run(context, 1, new SeedPresetShapeGroups { EveryoneGroupRate = 0 });

        Assert.Null(context.Registry.EveryoneGroupId);
        Assert.DoesNotContain(context.Groups, g => g.Name == "Everyone");
    }

    [Fact]
    public void Execute_HotGroupSizes_GivesEachHotGroupItsExactSize()
    {
        var context = ContextWithMembers(200);

        Run(context, 3, new SeedPresetShapeGroups { HotGroupSizes = [50, 20, 10] });

        Assert.Equal([50, 20, 10], context.Registry.HotGroupIds.Select(id => context.Registry.GroupMemberCounts[id]));
    }

    [Fact]
    public void Execute_HotGroupWithParent_DrawsOverlapShareOfMembersFromParent()
    {
        var context = ContextWithMembers(1000);

        Run(context, 3, new SeedPresetShapeGroups
        {
            HotGroupSizes = [60, 40, 20],
            HotGroupParents = [-1, -1, 1],
            HotGroupOverlap = 0.5,
            // Near-equal affinities, so heavy-affinity members don't land in both groups by preference
            UserAffinityAlpha = 1000,
        });

        // round(20 × 0.5) = 10 come from Hot Group 2. The other 10 are drawn org-wide, where Hot Group 2's
        // remaining 30 members are 3% of the org, so chance adds at most a couple more.
        var fromParent = MembersOf(context, "Hot Group 3").Intersect(MembersOf(context, "Hot Group 2")).Count();
        Assert.InRange(fromParent, 10, 12);
    }

    [Fact]
    public void Execute_TightMaxGroupsPerUser_NoMemberExceedsCapIncludingEveryoneGroup()
    {
        var context = ContextWithMembers(50);

        Run(context, 13, new SeedPresetShapeGroups
        {
            EveryoneGroupRate = 1.0,
            HotGroupSizes = [40, 10],
            Sizes = [new SeedPresetSizeBucket { Min = 20, Max = 20, Weight = 1 }],
            MaxGroupsPerUser = 2,
        });

        Assert.All(
            context.GroupUsers.GroupBy(gu => gu.OrganizationUserId),
            memberships => Assert.True(memberships.Count() <= 2));
    }

    [Fact]
    public void Execute_NestedGroupRateOne_RecordsChildrenDrawnFromParentMembers()
    {
        var context = ContextWithMembers(200);

        // 8 regular groups: exactly 2 of 50 members and 6 of 5. Every 5-member group nests under a 50-member one.
        Run(context, 8, new SeedPresetShapeGroups
        {
            NestedGroupRate = 1.0,
            Sizes =
            [
                new SeedPresetSizeBucket { Min = 50, Max = 50, Weight = 0.25 },
                new SeedPresetSizeBucket { Min = 5, Max = 5, Weight = 0.75 },
            ],
        });

        var names = context.Groups.ToDictionary(g => g.Id, g => g.Name);
        var links = context.Registry.GroupChildren
            .SelectMany(kv => kv.Value.Select(child => (Parent: names[kv.Key], Child: names[child])))
            .ToList();
        Assert.Equal(6, links.Count);
        Assert.All(links, link => Assert.Subset(MembersOf(context, link.Parent), MembersOf(context, link.Child)));
    }

    [Fact]
    public void Execute_DepartmentLocality_InDepartmentShareOfMembershipsIsNearLocality()
    {
        var context = ContextWithMembers(2000);
        var shape = new SeedPresetAccessShape
        {
            Departments = new SeedPresetShapeDepartments { Count = 4, Locality = 0.8 },
            // A huge alpha makes every affinity ~1, so the locality weighting alone decides who is picked
            Groups = new SeedPresetShapeGroups
            {
                UserAffinityAlpha = 1000,
                Sizes = [new SeedPresetSizeBucket { Min = 20, Max = 20, Weight = 1 }],
            },
        };

        new CreateShapedGroupsStep(100, shape, seed: 1).Execute(context);

        var memberIndex = context.Registry.HardenedOrgUserIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var inDepartment = context.GroupUsers.Count(gu =>
            context.Registry.MemberDepartments[memberIndex[gu.OrganizationUserId]] ==
            context.Registry.GroupDepartments[gu.GroupId]);
        var share = (double)inDepartment / context.GroupUsers.Count;
        // 2,000 picks give a binomial standard deviation of sqrt(0.8 × 0.2 / 2000) ≈ 0.009, so ±0.05 is over 5σ.
        // Uneven department sizes (500 ± ~20 members) shift a single group's share by about 1 point at most.
        // Without locality the share would be 0.25.
        Assert.InRange(share, 0.75, 0.85);
    }

    [Fact]
    public void Execute_SameSeed_GivesIdenticalMembership()
    {
        var members = NewMembers(300);
        var shape = MixedShape();

        var first = Membership(members, shape, seed: 5);
        var second = Membership(members, shape, seed: 5);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Execute_DifferentSeed_GivesDifferentMembership()
    {
        var members = NewMembers(300);
        var shape = MixedShape();

        var first = Membership(members, shape, seed: 5);
        var second = Membership(members, shape, seed: 6);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Execute_CountBelowEveryoneAndHotGroups_Throws()
    {
        var context = ContextWithMembers(50);
        var groups = new SeedPresetShapeGroups { EveryoneGroupRate = 0.5, HotGroupSizes = [5, 5] };

        var ex = Assert.Throws<InvalidOperationException>(() => Run(context, 2, groups));

        Assert.Contains("groups.count (2) is smaller than the everyone group plus 2 hot groups", ex.Message);
    }

    [Fact]
    public void Execute_HotGroupLargerThanMembers_Throws()
    {
        var context = ContextWithMembers(50);

        var ex = Assert.Throws<InvalidOperationException>(() => Run(context, 1, new SeedPresetShapeGroups { HotGroupSizes = [60] }));

        Assert.Contains("50 members in Hot Group 1 but the preset asks for 60", ex.Message);
    }

    [Fact]
    public void Execute_MixedShape_RegistryMatchesCreatedGroupsAndMemberships()
    {
        var context = ContextWithMembers(300);
        var shape = MixedShape();

        new CreateShapedGroupsStep(30, shape, seed: 5).Execute(context);

        var registry = context.Registry;
        Assert.Equal(context.Groups.Select(g => g.Id), registry.GroupIds);
        Assert.All(context.Groups, g =>
            Assert.Equal(context.GroupUsers.Count(gu => gu.GroupId == g.Id), registry.GroupMemberCounts[g.Id]));
        Assert.Equal(300, registry.MemberDepartments.Count);
        Assert.Equal("Everyone", context.Groups.Single(g => g.Id == registry.EveryoneGroupId).Name);
        Assert.Equal(
            ["Hot Group 1", "Hot Group 2", "Hot Group 3"],
            registry.HotGroupIds.Select(id => context.Groups.Single(g => g.Id == id).Name));
    }

    [Theory]
    [InlineData(0.8, 4)]
    [InlineData(0.5, 2)]
    [InlineData(0.3, 10)]
    [InlineData(1.0, 3)]
    public void OutOfDepartmentWeight_EqualAffinities_GivesInDepartmentShareEqualToLocality(double locality, int departmentCount)
    {
        var weight = CreateShapedGroupsStep.OutOfDepartmentWeight(locality, departmentCount);

        Assert.Equal(locality, 1 / (1 + (departmentCount - 1) * weight), 12);
    }

    [Fact]
    public void OutOfDepartmentWeight_ZeroLocality_WeighsEveryMemberEqually()
    {
        Assert.Equal(1.0, CreateShapedGroupsStep.OutOfDepartmentWeight(0, 4));
    }

    [Theory]
    [InlineData(null, 0, -1)]
    [InlineData(null, 2, 0)]
    [InlineData(new[] { -1 }, 1, 0)]
    public void ResolveHotParent_NoConfiguredEntry_FirstHasNoneAndLaterOnesUseTheFirst(int[]? parents, int index, int expected)
    {
        Assert.Equal(expected, CreateShapedGroupsStep.ResolveHotParent(parents, index));
    }

    /// <summary>
    /// Everyone group, chained hot groups, nested regular groups, departments and a cap that bites.
    /// </summary>
    private static SeedPresetAccessShape MixedShape() => new()
    {
        Departments = new SeedPresetShapeDepartments { Count = 4, Locality = 0.8 },
        Groups = new SeedPresetShapeGroups
        {
            EveryoneGroupRate = 0.9,
            HotGroupSizes = [120, 60, 30],
            HotGroupParents = [-1, 0, 1],
            HotGroupOverlap = 0.5,
            NestedGroupRate = 0.3,
            MaxGroupsPerUser = 4,
            Sizes =
            [
                new SeedPresetSizeBucket { Min = 1, Max = 5, Weight = 0.6 },
                new SeedPresetSizeBucket { Min = 6, Max = 80, Weight = 0.4 },
            ],
        },
    };

    private static List<(string Group, int Member)> Membership(List<Guid> members, SeedPresetAccessShape shape, int seed)
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        context.Registry.HardenedOrgUserIds.AddRange(members);

        new CreateShapedGroupsStep(30, shape, seed).Execute(context);

        var names = context.Groups.ToDictionary(g => g.Id, g => g.Name);
        return context.GroupUsers.Select(gu => (names[gu.GroupId], members.IndexOf(gu.OrganizationUserId))).ToList();
    }

    private static void Run(SeederContext context, int count, SeedPresetShapeGroups groups) =>
        new CreateShapedGroupsStep(count, new SeedPresetAccessShape { Groups = groups }, seed: 1).Execute(context);

    private static SeederContext ContextWithMembers(int memberCount)
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        context.Registry.HardenedOrgUserIds.AddRange(NewMembers(memberCount));
        return context;
    }

    private static List<Guid> NewMembers(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    private static HashSet<Guid> MembersOf(SeederContext context, string groupName)
    {
        var groupId = context.Groups.Single(g => g.Name == groupName).Id;
        return context.GroupUsers.Where(gu => gu.GroupId == groupId).Select(gu => gu.OrganizationUserId).ToHashSet();
    }
}
