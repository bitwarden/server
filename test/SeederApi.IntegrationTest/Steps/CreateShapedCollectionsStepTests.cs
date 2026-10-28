using Bit.Core.AdminConsole.Entities;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class CreateShapedCollectionsStepTests
{
    [Fact]
    public void Execute_TotalAssignments_SizesSumExactlyToTotal()
    {
        var context = ContextWithMembers(20);

        Run(context, 50, new SeedPresetAccessShape
        {
            Collections = new SeedPresetShapeCollections
            {
                Sizes = [new SeedPresetSizeBucket { Min = 1, Max = 40, Weight = 1 }],
                TotalAssignments = 777,
            },
            GroupGrants = new SeedPresetShapeGroupGrants { HotCollections = [100, 60] },
        });

        Assert.Equal(777, context.Registry.CollectionTargetSizes.Sum());
    }

    [Fact]
    public void Execute_HotCollections_TakeTheirExactSizesFirst()
    {
        var context = ContextWithMembers(20);

        Run(context, 50, new SeedPresetAccessShape
        {
            Collections = new SeedPresetShapeCollections
            {
                Sizes = [new SeedPresetSizeBucket { Min = 1, Max = 40, Weight = 1 }],
                TotalAssignments = 777,
            },
            GroupGrants = new SeedPresetShapeGroupGrants { HotCollections = [100, 60, 3] },
        });

        Assert.Equal([100, 60, 3], context.Registry.CollectionTargetSizes.Take(3));
    }

    [Fact]
    public void Execute_CountBelowHotCollections_Throws()
    {
        var context = ContextWithMembers(20);
        var shape = new SeedPresetAccessShape { GroupGrants = new SeedPresetShapeGroupGrants { HotCollections = [10, 10, 10] } };

        var ex = Assert.Throws<InvalidOperationException>(() => Run(context, 2, shape));

        Assert.Contains("collections.count (2) is smaller than the 3 hot-pair collections", ex.Message);
    }

    [Fact]
    public void Execute_HotPairs_GrantEachHotGroupExactlyItsListedCollections()
    {
        var shape = new SeedPresetAccessShape
        {
            Groups = new SeedPresetShapeGroups { HotGroupSizes = [20, 10, 5] },
            GroupGrants = new SeedPresetShapeGroupGrants
            {
                HotCollections = [40, 30, 20, 10],
                HotPairs =
                [
                    new SeedPresetShapeHotPair { Group = 0, Collections = [0, 1] },
                    new SeedPresetShapeHotPair { Group = 1, Collections = [1, 2] },
                    new SeedPresetShapeHotPair { Group = 2, Collections = [3] },
                ],
            },
        };
        var context = ContextWithGroups(100, 3, shape);

        Run(context, 20, shape);

        var hotGroups = context.Registry.HotGroupIds;
        Assert.Equal([0, 1], GrantedCollections(context, hotGroups[0]));
        Assert.Equal([1, 2], GrantedCollections(context, hotGroups[1]));
        Assert.Equal([3], GrantedCollections(context, hotGroups[2]));
    }

    [Fact]
    public void Execute_EveryoneGroupCollections_GrantsEveryoneThatManyCollectionsOfOneOrTwoItems()
    {
        var shape = new SeedPresetAccessShape
        {
            Groups = new SeedPresetShapeGroups { EveryoneGroupRate = 0.8 },
            Collections = new SeedPresetShapeCollections
            {
                Sizes =
                [
                    new SeedPresetSizeBucket { Min = 0, Max = 0, Weight = 1 },
                    new SeedPresetSizeBucket { Min = 1, Max = 2, Weight = 1 },
                    new SeedPresetSizeBucket { Min = 3, Max = 50, Weight = 2 },
                ],
            },
            GroupGrants = new SeedPresetShapeGroupGrants { EveryoneGroupCollections = 5 },
        };
        var context = ContextWithGroups(50, 1, shape);

        Run(context, 40, shape);

        var granted = GrantedCollections(context, context.Registry.EveryoneGroupId!.Value);
        Assert.Equal(5, granted.Count);
        Assert.All(granted, c => Assert.InRange(context.Registry.CollectionTargetSizes[c], 1, 2));
    }

    [Theory]
    [InlineData(30, 20, 20)]
    [InlineData(10, 20, 10)]
    public void Execute_WideGroups_FirstWideGroupGetsMaxGrantsCappedAtBlockSize(int maxGrants, int blockSize, int expected)
    {
        var shape = WideGroupShape(new SeedPresetShapeWideGroups
        {
            Count = 3,
            BlockSize = blockSize,
            BlockMaxItems = 2,
            MinGrants = 2,
            MaxGrants = maxGrants,
        });
        var context = ContextWithGroups(100, 30, shape);

        Run(context, 60, shape);

        // No hot, everyone or random grants, so every row is a wide grant. Later wide groups draw at most
        // MaxGrants, so the largest grant count is the first wide group's.
        Assert.Equal(expected, context.CollectionGroups.GroupBy(cg => cg.GroupId).Max(g => g.Count()));
    }

    [Fact]
    public void Execute_WideGroups_GrantOnlyCollectionsWithAtMostBlockMaxItems()
    {
        var shape = WideGroupShape(new SeedPresetShapeWideGroups
        {
            Count = 3,
            BlockSize = 15,
            BlockMaxItems = 3,
            MinGrants = 5,
            MaxGrants = 15,
        });
        var context = ContextWithGroups(100, 30, shape);

        Run(context, 60, shape);

        Assert.NotEmpty(context.CollectionGroups);
        var index = CollectionIndex(context);
        Assert.All(context.CollectionGroups, cg =>
            Assert.True(context.Registry.CollectionTargetSizes[index[cg.CollectionId]] <= 3));
    }

    [Fact]
    public void Execute_WideGroups_PickOnlyGroupsWithAtMostMaxMembers()
    {
        var shape = WideGroupShape(new SeedPresetShapeWideGroups
        {
            Count = 5,
            BlockSize = 15,
            BlockMaxItems = 3,
            MinGrants = 5,
            MaxGrants = 15,
            MaxMembers = 3,
        });
        var context = ContextWithGroups(100, 30, shape);

        Run(context, 60, shape);

        var wideGroups = context.CollectionGroups.Select(cg => cg.GroupId).Distinct().ToList();
        Assert.Equal(5, wideGroups.Count);
        Assert.All(wideGroups, g => Assert.InRange(context.Registry.GroupMemberCounts[g], 1, 3));
    }

    [Fact]
    public void Execute_GroupGrantTotal_CountsEveryGrantKindAndStopsAtTotal()
    {
        var shape = MixedShape();
        var context = ContextWithGroups(300, 40, shape);

        Run(context, 120, shape);

        // The random pass stops as soon as hot, everyone, wide, random and echo grants together reach the total
        Assert.Equal(shape.GroupGrants!.Total, context.CollectionGroups.Count);
    }

    [Fact]
    public void Execute_GroupGrants_HaveNoDuplicateCollectionGroupPairs()
    {
        var shape = MixedShape();
        var context = ContextWithGroups(300, 40, shape);

        Run(context, 120, shape);

        Assert.Equal(context.CollectionGroups.Count, context.CollectionGroups.Select(cg => (cg.CollectionId, cg.GroupId)).Distinct().Count());
    }

    [Fact]
    public void Execute_MaxGrantsPerGroup_NoGroupExceedsCap()
    {
        // Only regular groups and no echo, so every row is a capped random grant; hot, everyone and wide grants
        // are excluded by building none
        var shape = new SeedPresetAccessShape
        {
            Groups = new SeedPresetShapeGroups { Sizes = [new SeedPresetSizeBucket { Min = 5, Max = 20, Weight = 1 }] },
            GroupGrants = new SeedPresetShapeGroupGrants { Total = 50, MaxGrantsPerGroup = 3, GroupAppetiteAlpha = 0.8 },
        };
        var context = ContextWithGroups(100, 20, shape);

        Run(context, 40, shape);

        Assert.All(context.CollectionGroups.GroupBy(cg => cg.GroupId), g => Assert.True(g.Count() <= 3));
    }

    [Fact]
    public void Execute_GrantedGroupRate_GrantsOnlyTheLargestRegularGroups()
    {
        var context = ContextWithMembers(20);
        var groups = AddRegisteredGroups(context, [4, 9, 1, 7, 10, 2, 8, 3, 6, 5]);

        Run(context, 20, new SeedPresetAccessShape
        {
            GroupGrants = new SeedPresetShapeGroupGrants { Total = 15, GrantedGroupRate = 0.3 },
        });

        // round(10 × 0.3) = 3: the groups of 10, 9 and 8 members
        Assert.Equal(
            new[] { groups[4], groups[1], groups[6] }.ToHashSet(),
            context.CollectionGroups.Select(cg => cg.GroupId).ToHashSet());
    }

    [Fact]
    public void Execute_EchoWithNestedChild_CopiesTheSourceGrantToTheChild()
    {
        var context = ContextWithMembers(20);
        var groups = AddRegisteredGroups(context, [10, 3]);
        context.Registry.GroupChildren[groups[0]] = [groups[1]];

        // Rate 0.5 of 2 groups leaves only the source in the random pass. EchoRate 1 makes every attempt after the
        // first random grant an echo, so the child gets nothing but echoes.
        Run(context, 30, new SeedPresetAccessShape
        {
            GroupGrants = new SeedPresetShapeGroupGrants { Total = 2, GrantedGroupRate = 0.5, EchoRate = 1 },
        });

        Assert.Equal(GrantedCollections(context, groups[0]), GrantedCollections(context, groups[1]));
        Assert.Single(GrantedCollections(context, groups[1]));
    }

    [Fact]
    public void Execute_EchoWithoutChild_CopiesToOverlappingPeersNoBiggerThanTheSource()
    {
        var context = ContextWithMembers(20);
        var groups = AddRegisteredGroups(context, [10, 4, 20]);
        var (source, smallPeer, bigPeer) = (groups[0], groups[1], groups[2]);
        // The big peer is a hot group so the random pass skips it; rate 0.5 of the other two leaves only the source
        context.Registry.HotGroupIds.Add(bigPeer);
        var (m1, m2) = (context.Registry.HardenedOrgUserIds[0], context.Registry.HardenedOrgUserIds[1]);
        context.GroupUsers.AddRange(
        [
            new GroupUser { GroupId = source, OrganizationUserId = m1 },
            new GroupUser { GroupId = smallPeer, OrganizationUserId = m1 },
            new GroupUser { GroupId = source, OrganizationUserId = m2 },
            new GroupUser { GroupId = bigPeer, OrganizationUserId = m2 },
        ]);

        Run(context, 60, new SeedPresetAccessShape
        {
            GroupGrants = new SeedPresetShapeGroupGrants { Total = 40, GrantedGroupRate = 0.5, EchoRate = 0.5 },
        });

        var echoed = GrantedCollections(context, smallPeer);
        Assert.NotEmpty(echoed);
        Assert.Subset(GrantedCollections(context, source).ToHashSet(), echoed.ToHashSet());
        Assert.Empty(GrantedCollections(context, bigPeer));
    }

    [Fact]
    public void Execute_GroupSizeBias_ShiftsRandomGrantsTowardSmallGroups()
    {
        // Ten 2-member and ten 50-member groups. Weight = members^-bias: bias 0 gives each half 0.5; bias 1 gives the
        // small half 0.5 / (0.5 + 0.02) ≈ 0.96. 200 grants give a standard deviation of 0.035 and 0.014.
        double SmallGroupShare(double? bias)
        {
            var context = ContextWithMembers(20);
            var groups = AddRegisteredGroups(context, [.. Enumerable.Repeat(2, 10), .. Enumerable.Repeat(50, 10)]);
            Run(context, 100, new SeedPresetAccessShape
            {
                GroupGrants = new SeedPresetShapeGroupGrants { Total = 200, GroupSizeBias = bias },
            });
            var small = groups.Take(10).ToHashSet();
            return (double)context.CollectionGroups.Count(cg => small.Contains(cg.GroupId)) / context.CollectionGroups.Count;
        }

        Assert.InRange(SmallGroupShare(null), 0.38, 0.62);
        Assert.InRange(SmallGroupShare(1), 0.9, 1.0);
    }

    [Fact]
    public void Execute_LowCollectionPopularityAlpha_ConcentratesGrantsOnFewerCollections()
    {
        double TopTenthShare(double? alpha)
        {
            var context = ContextWithMembers(20);
            AddRegisteredGroups(context, Enumerable.Repeat(5, 100).ToArray());
            Run(context, 200, new SeedPresetAccessShape
            {
                GroupGrants = new SeedPresetShapeGroupGrants { Total = 1000, CollectionPopularityAlpha = alpha },
            });
            var top = context.CollectionGroups.GroupBy(cg => cg.CollectionId).Select(g => g.Count())
                .OrderDescending().Take(20).Sum();
            return (double)top / context.CollectionGroups.Count;
        }

        // Uniform: 5 grants per collection on average, so the busiest 20 of 200 hold about 0.17. Pareto(1) weights put
        // over half of the weight on the top tenth; each collection still caps at the 100 groups.
        Assert.InRange(TopTenthShare(null), 0.1, 0.25);
        Assert.InRange(TopTenthShare(1.0), 0.4, 1.0);
    }

    [Fact]
    public void Execute_NegativeCollectionSizeBias_FavorsSmallCollectionsForGroupGrants()
    {
        // Half the collections hold 1 item, half 50. Weight = (items + 1)^bias: bias 0 gives each half 0.5; bias -1
        // gives the small half 0.5 / (0.5 + 1/51) ≈ 0.96. 300 grants give a standard deviation of 0.029 and 0.011.
        double SmallCollectionShare(double? bias)
        {
            var context = ContextWithMembers(20);
            AddRegisteredGroups(context, Enumerable.Repeat(5, 20).ToArray());
            Run(context, 100, new SeedPresetAccessShape
            {
                Collections = new SeedPresetShapeCollections
                {
                    Sizes =
                    [
                        new SeedPresetSizeBucket { Min = 1, Max = 1, Weight = 1 },
                        new SeedPresetSizeBucket { Min = 50, Max = 50, Weight = 1 },
                    ],
                },
                GroupGrants = new SeedPresetShapeGroupGrants { Total = 300, CollectionSizeBias = bias },
            });
            var index = CollectionIndex(context);
            return (double)context.CollectionGroups.Count(cg => context.Registry.CollectionTargetSizes[index[cg.CollectionId]] == 1)
                / context.CollectionGroups.Count;
        }

        Assert.InRange(SmallCollectionShare(null), 0.4, 0.6);
        Assert.InRange(SmallCollectionShare(-1), 0.9, 1.0);
    }

    [Fact]
    public void Execute_GroupGrantsBeyondItsDepartment_SpillOverOrgWide()
    {
        var context = ContextWithMembers(20);
        var group = AddRegisteredGroups(context, [5])[0];
        context.Registry.GroupDepartments[group] = 0;
        context.Registry.DepartmentLocality = 1.0;

        Run(context, 20, new SeedPresetAccessShape
        {
            Departments = new SeedPresetShapeDepartments { Count = 10 },
            GroupGrants = new SeedPresetShapeGroupGrants { Total = 10 },
        });

        // Locality 1 sends every first pick into department 0, which holds fewer than 10 collections
        var inDepartment = context.Registry.CollectionDepartments.Count(d => d == 0);
        Assert.InRange(inDepartment, 1, 9);
        Assert.Equal(10, GrantedCollections(context, group).Count);
    }

    [Fact]
    public void Execute_UngroupedBoost_RaisesUngroupedMembersShareOfDirectGrants()
    {
        // Half the members are in a group. Equal affinities give the ungrouped half 0.5 of the picks; a boost of 9 gives
        // them 9 / 10 = 0.9. 1,000 grants give a standard deviation of 0.016 and 0.009.
        double UngroupedShare(double? boost)
        {
            var context = ContextWithMembers(200);
            var group = AddRegisteredGroups(context, [100])[0];
            var grouped = context.Registry.HardenedOrgUserIds.Take(100).ToHashSet();
            context.GroupUsers.AddRange(grouped.Select(m => new GroupUser { GroupId = group, OrganizationUserId = m }));
            Run(context, 50, new SeedPresetAccessShape
            {
                DirectGrants = new SeedPresetShapeDirectGrants { Total = 1000, UserAffinityAlpha = 1000, UngroupedBoost = boost },
            });
            return (double)context.CollectionUsers.Count(cu => !grouped.Contains(cu.OrganizationUserId)) / context.CollectionUsers.Count;
        }

        Assert.InRange(UngroupedShare(null), 0.42, 0.58);
        Assert.InRange(UngroupedShare(9), 0.85, 0.95);
    }

    [Fact]
    public void Execute_DirectGrantTotal_CreatesExactlyThatManyRows()
    {
        var context = ContextWithMembers(50);

        Run(context, 30, new SeedPresetAccessShape { DirectGrants = new SeedPresetShapeDirectGrants { Total = 400 } });

        Assert.Equal(400, context.CollectionUsers.Count);
    }

    [Fact]
    public void Execute_DirectGrants_HaveNoDuplicateCollectionMemberPairs()
    {
        // 90 of the 100 possible pairs, so duplicate draws are frequent
        var context = ContextWithMembers(10);

        Run(context, 10, new SeedPresetAccessShape { DirectGrants = new SeedPresetShapeDirectGrants { Total = 90 } });

        Assert.Equal(90, context.CollectionUsers.Select(cu => (cu.CollectionId, cu.OrganizationUserId)).Distinct().Count());
    }

    [Fact]
    public void Execute_DepartmentLocality_InDepartmentShareOfGroupGrantsIsNearExpected()
    {
        var shape = LocalityShape(new SeedPresetShapeGroupGrants { Total = 2000 }, directGrants: null);
        var context = ContextWithGroups(400, 400, shape);

        Run(context, 400, shape);

        var index = CollectionIndex(context);
        var registry = context.Registry;
        var inDepartment = context.CollectionGroups.Count(cg =>
            registry.GroupDepartments[cg.GroupId] == registry.CollectionDepartments[index[cg.CollectionId]]);
        // A group's repeat local pick retries org-wide. Five grants per group from ~100 in-department collections
        // repeat for ~2% of local picks, which lowers the expected share by about 0.01 to 0.84.
        Assert.InRange((double)inDepartment / context.CollectionGroups.Count, 0.80, 0.88);
    }

    [Fact]
    public void Execute_DepartmentLocality_InDepartmentShareOfDirectGrantsIsNearExpected()
    {
        var shape = LocalityShape(groupGrants: null, new SeedPresetShapeDirectGrants { Total = 2000 });
        var context = ContextWithGroups(400, 10, shape);

        Run(context, 400, shape);

        var index = CollectionIndex(context);
        var memberIndex = MemberIndex(context);
        var registry = context.Registry;
        var inDepartment = context.CollectionUsers.Count(cu =>
            registry.MemberDepartments[memberIndex[cu.OrganizationUserId]] == registry.CollectionDepartments[index[cu.CollectionId]]);
        Assert.InRange((double)inDepartment / context.CollectionUsers.Count, 0.81, 0.89);
    }

    [Fact]
    public void Execute_InactiveAccessRate_GivesAccessToTheMembersItsOwnStreamPicks()
    {
        var context = ContextWithOneGroupPerMember(confirmed: 40, inactive: 400);

        Run(context, 20, new SeedPresetAccessShape { InactiveAccessRate = 0.25 }, seed: 7);

        // Inactive access draws from new Random(seed + 3): one NextDouble per inactive member, plus one Next
        // for the twin when it lands under the rate
        var random = new Random(7 + 3);
        var expected = context.Registry.InactiveOrgUserIds.Where(_ =>
        {
            var picked = random.NextDouble() < 0.25;
            if (picked)
            {
                random.Next(40);
            }
            return picked;
        }).ToHashSet();
        var inactive = context.Registry.InactiveOrgUserIds.ToHashSet();
        var withAccess = context.GroupUsers.Select(gu => gu.OrganizationUserId).Where(inactive.Contains).ToHashSet();
        Assert.Equal(expected, withAccess);
        // 400 × 0.25 = 100, with a binomial standard deviation of about 8.7
        Assert.InRange(withAccess.Count, 70, 130);
    }

    [Fact]
    public void Execute_InactiveMember_CopiesOneConfirmedMembersNonEveryoneGroupsAndDirectGrants()
    {
        var shape = new SeedPresetAccessShape
        {
            Groups = new SeedPresetShapeGroups
            {
                EveryoneGroupRate = 1.0,
                Sizes = [new SeedPresetSizeBucket { Min = 3, Max = 15, Weight = 1 }],
            },
            DirectGrants = new SeedPresetShapeDirectGrants { Total = 150 },
            InactiveAccessRate = 0.5,
        };
        var context = ContextWithGroups(60, 20, shape, inactive: 40);

        Run(context, 30, shape);

        var registry = context.Registry;
        var everyone = registry.EveryoneGroupId!.Value;
        string Access(Guid member) =>
            string.Join(",", context.GroupUsers.Where(gu => gu.OrganizationUserId == member && gu.GroupId != everyone)
                .Select(gu => gu.GroupId.ToString()).Order()) + "|" +
            string.Join(",", context.CollectionUsers.Where(cu => cu.OrganizationUserId == member)
                .Select(cu => $"{cu.CollectionId}:{cu.ReadOnly}:{cu.HidePasswords}:{cu.Manage}").Order());
        var confirmedAccess = registry.HardenedOrgUserIds.Select(Access).ToHashSet();
        var copied = registry.InactiveOrgUserIds.Where(m => context.GroupUsers.Any(gu => gu.OrganizationUserId == m)).ToList();
        Assert.NotEmpty(copied);
        Assert.DoesNotContain(context.GroupUsers, gu => gu.GroupId == everyone && registry.InactiveOrgUserIds.Contains(gu.OrganizationUserId));
        Assert.All(copied, m => Assert.Contains(Access(m), confirmedAccess));
    }

    [Fact]
    public void Execute_InactiveAccessRate_LeavesConfirmedMembersShapeUnchanged()
    {
        var members = NewIds(200);
        var inactive = NewIds(50);
        var shape = MixedShape();

        var without = Output(members, inactive, shape with { InactiveAccessRate = 0 }, seed: 3, confirmedOnly: true);
        var with = Output(members, inactive, shape with { InactiveAccessRate = 0.6 }, seed: 3, confirmedOnly: true);

        Assert.Equal(without, with);
    }

    [Fact]
    public void Execute_DirectGrantTotalAboveMembersTimesCollections_Throws()
    {
        var context = ContextWithMembers(5);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Run(context, 4, new SeedPresetAccessShape { DirectGrants = new SeedPresetShapeDirectGrants { Total = 21 } }));

        Assert.Contains("20 direct grants but the preset asks for 21", ex.Message);
    }

    [Fact]
    public void Execute_GroupGrantTotalAboveCaps_Throws()
    {
        var shape = new SeedPresetAccessShape
        {
            Groups = new SeedPresetShapeGroups { Sizes = [new SeedPresetSizeBucket { Min = 5, Max = 5, Weight = 1 }] },
            GroupGrants = new SeedPresetShapeGroupGrants { Total = 31, MaxGrantsPerGroup = 3 },
        };
        var context = ContextWithGroups(50, 10, shape);

        var ex = Assert.Throws<InvalidOperationException>(() => Run(context, 40, shape));

        Assert.Contains("30 group grants but the preset asks for 31", ex.Message);
    }

    [Fact]
    public void Execute_PinnedGrantsAboveGroupGrantTotal_Throws()
    {
        var shape = new SeedPresetAccessShape
        {
            Groups = new SeedPresetShapeGroups { HotGroupSizes = [10] },
            GroupGrants = new SeedPresetShapeGroupGrants
            {
                Total = 2,
                HotCollections = [5, 5, 5],
                HotPairs = [new SeedPresetShapeHotPair { Group = 0, Collections = [0, 1, 2] }],
            },
        };
        var context = ContextWithGroups(50, 1, shape);

        var ex = Assert.Throws<InvalidOperationException>(() => Run(context, 10, shape));

        Assert.Contains("made 3 hot-pair, everyone-group and wide-group grants, more than groupGrants.total (2)", ex.Message);
    }

    [Fact]
    public void Execute_SameSeed_GivesIdenticalOutput()
    {
        var members = NewIds(200);
        var inactive = NewIds(50);
        var shape = MixedShape() with { InactiveAccessRate = 0.5 };

        Assert.Equal(Output(members, inactive, shape, seed: 5), Output(members, inactive, shape, seed: 5));
    }

    [Fact]
    public void Execute_DifferentSeed_GivesDifferentOutput()
    {
        var members = NewIds(200);
        var inactive = NewIds(50);
        var shape = MixedShape() with { InactiveAccessRate = 0.5 };

        Assert.NotEqual(Output(members, inactive, shape, seed: 5), Output(members, inactive, shape, seed: 6));
    }

    [Fact]
    public void Execute_RegistersAlignedCollectionIdsSizesAndDepartments()
    {
        var shape = MixedShape();
        var context = ContextWithGroups(300, 40, shape);

        Run(context, 120, shape);

        var registry = context.Registry;
        Assert.Equal(context.Collections.Select(c => c.Id), registry.CollectionIds);
        Assert.Equal(120, registry.CollectionTargetSizes.Count);
        Assert.Equal(120, registry.CollectionDepartments.Count);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(3, 3)]
    public void Execute_MaxCollectionsPerCipher_IsCopiedToRegistry(int? configured, int expected)
    {
        var context = ContextWithMembers(10);
        context.Registry.MaxCollectionsPerCipher = 7;

        Run(context, 5, new SeedPresetAccessShape
        {
            Collections = new SeedPresetShapeCollections { MaxCollectionsPerCipher = configured },
        });

        Assert.Equal(expected, context.Registry.MaxCollectionsPerCipher);
    }

    [Fact]
    public void HotCollectionGroups_FirstListingPairWinsAndUnlistedCollectionsHaveNone()
    {
        List<SeedPresetShapeHotPair> pairs =
        [
            new SeedPresetShapeHotPair { Group = 2, Collections = [1, 3] },
            new SeedPresetShapeHotPair { Group = 0, Collections = [0, 1] },
        ];

        Assert.Equal(new[] { 0, 2, -1, 2 }, CreateShapedCollectionsStep.HotCollectionGroups(4, pairs));
    }

    [Theory]
    [InlineData(5, 5, 0.0, 5)]
    [InlineData(5, 5, 0.5, 5)]
    [InlineData(5, 5, 0.999, 5)]
    [InlineData(12, 5, 0.5, 12)]
    [InlineData(0, 0, 0.3, 1)]
    public void LogUniformGrantCount_MaxNotAboveMin_ReturnsRaisedMin(int minGrants, int maxGrants, double u, int expected)
    {
        Assert.Equal(expected, CreateShapedCollectionsStep.LogUniformGrantCount(minGrants, maxGrants, u));
    }

    [Fact]
    public void LogUniformGrantCount_ZeroDraw_ReturnsMin()
    {
        // Exp(Log(5)) is just under 5, so the floor alone gives 4
        Assert.Equal(5, CreateShapedCollectionsStep.LogUniformGrantCount(5, 20, 0.0));
    }

    [Theory]
    [InlineData(5, 20)]
    [InlineData(2, 40)]
    [InlineData(0, 10)]
    public void LogUniformGrantCount_DistinctBounds_StaysAtOrAboveMinAndBelowMax(int minGrants, int maxGrants)
    {
        var low = Math.Max(minGrants, 1);

        var counts = Enumerable.Range(0, 1000).Select(i => CreateShapedCollectionsStep.LogUniformGrantCount(minGrants, maxGrants, i / 1000.0));

        Assert.All(counts, n => Assert.InRange(n, low, maxGrants - 1));
    }

    [Fact]
    public void OverlappingGroups_ListsGroupsSharingAMemberExceptEveryone()
    {
        var (a, b, c, everyone) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (m1, m2, m3) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        List<GroupUser> groupUsers =
        [
            new() { GroupId = a, OrganizationUserId = m1 },
            new() { GroupId = b, OrganizationUserId = m1 },
            new() { GroupId = c, OrganizationUserId = m2 },
            new() { GroupId = everyone, OrganizationUserId = m1 },
            new() { GroupId = everyone, OrganizationUserId = m2 },
            new() { GroupId = a, OrganizationUserId = m3 },
        ];

        var peers = CreateShapedCollectionsStep.OverlappingGroups(groupUsers, everyone);

        Assert.Equal(new[] { a, b }.ToHashSet(), peers.Keys.ToHashSet());
        Assert.Equal(new[] { b }, peers[a]);
        Assert.Equal(new[] { a }, peers[b]);
    }

    /// <summary>
    /// Everyone, hot and nested groups with departments, hot pairs, everyone and wide grants, appetite, popularity,
    /// echo and direct grants with an ungrouped boost.
    /// </summary>
    private static SeedPresetAccessShape MixedShape() => new()
    {
        Departments = new SeedPresetShapeDepartments { Count = 4, Locality = 0.7 },
        Groups = new SeedPresetShapeGroups
        {
            EveryoneGroupRate = 0.9,
            HotGroupSizes = [60, 30],
            NestedGroupRate = 0.3,
            MaxGroupsPerUser = 5,
            Sizes =
            [
                new SeedPresetSizeBucket { Min = 1, Max = 3, Weight = 0.5 },
                new SeedPresetSizeBucket { Min = 4, Max = 40, Weight = 0.5 },
            ],
        },
        Collections = new SeedPresetShapeCollections
        {
            Sizes =
            [
                new SeedPresetSizeBucket { Min = 0, Max = 0, Weight = 0.3 },
                new SeedPresetSizeBucket { Min = 1, Max = 2, Weight = 0.3 },
                new SeedPresetSizeBucket { Min = 3, Max = 60, Weight = 0.4 },
            ],
            TotalAssignments = 2500,
        },
        GroupGrants = new SeedPresetShapeGroupGrants
        {
            Total = 600,
            HotCollections = [200, 120],
            HotPairs =
            [
                new SeedPresetShapeHotPair { Group = 0, Collections = [0, 1] },
                new SeedPresetShapeHotPair { Group = 1, Collections = [1] },
            ],
            EveryoneGroupCollections = 4,
            WideGroups = new SeedPresetShapeWideGroups { Count = 2, BlockSize = 20, BlockMaxItems = 2, MinGrants = 5, MaxGrants = 20 },
            GroupAppetiteAlpha = 1.2,
            CollectionPopularityAlpha = 1.5,
            EchoRate = 0.4,
            MaxGrantsPerGroup = 60,
        },
        DirectGrants = new SeedPresetShapeDirectGrants { Total = 300, CollectionSizeBias = 0.5, UngroupedBoost = 4 },
    };

    private static SeedPresetAccessShape WideGroupShape(SeedPresetShapeWideGroups wide) => new()
    {
        Groups = new SeedPresetShapeGroups { Sizes = [new SeedPresetSizeBucket { Min = 1, Max = 10, Weight = 1 }] },
        Collections = new SeedPresetShapeCollections
        {
            Sizes =
            [
                new SeedPresetSizeBucket { Min = 0, Max = 3, Weight = 0.5 },
                new SeedPresetSizeBucket { Min = 10, Max = 50, Weight = 0.5 },
            ],
        },
        GroupGrants = new SeedPresetShapeGroupGrants { WideGroups = wide },
    };

    /// <summary>
    /// Four departments at locality 0.8 with uniform pick weights. A pick stays in its department with probability
    /// 0.8 and otherwise lands there by chance a quarter of the time, so about 0.8 + 0.2 × 0.25 = 0.85 of the grants
    /// are in-department (0.25 without locality). 2,000 grants give a binomial standard deviation of
    /// sqrt(0.85 × 0.15 / 2000) ≈ 0.008, so ±0.04 is about 5σ. Uneven department sizes (100 ± ~9 of 400 collections)
    /// move the chance share by under 0.005.
    /// </summary>
    private static SeedPresetAccessShape LocalityShape(SeedPresetShapeGroupGrants? groupGrants, SeedPresetShapeDirectGrants? directGrants) => new()
    {
        Departments = new SeedPresetShapeDepartments { Count = 4, Locality = 0.8 },
        Groups = new SeedPresetShapeGroups { Sizes = [new SeedPresetSizeBucket { Min = 5, Max = 5, Weight = 1 }] },
        GroupGrants = groupGrants,
        DirectGrants = directGrants is null ? null : directGrants with { UserAffinityAlpha = 1000 },
    };

    private static List<string> Output(List<Guid> members, List<Guid> inactive, SeedPresetAccessShape shape, int seed, bool confirmedOnly = false)
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        context.Registry.HardenedOrgUserIds.AddRange(members);
        context.Registry.InactiveOrgUserIds.AddRange(inactive);
        new CreateShapedGroupsStep(40, shape, seed: 1).Execute(context);
        var groupUserCount = context.GroupUsers.Count;

        Run(context, 120, shape, seed);

        var index = CollectionIndex(context);
        var names = context.Groups.ToDictionary(g => g.Id, g => g.Name);
        var memberIndex = members.Concat(inactive).Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var confirmed = members.ToHashSet();
        return context.CollectionGroups
            .Select(cg => $"cg {index[cg.CollectionId]} {names[cg.GroupId]} {cg.ReadOnly} {cg.HidePasswords} {cg.Manage}")
            .Concat(context.CollectionUsers.Where(cu => !confirmedOnly || confirmed.Contains(cu.OrganizationUserId))
                .Select(cu => $"cu {index[cu.CollectionId]} {memberIndex[cu.OrganizationUserId]} {cu.ReadOnly} {cu.HidePasswords} {cu.Manage}"))
            .Concat(context.GroupUsers.Skip(groupUserCount).Where(_ => !confirmedOnly)
                .Select(gu => $"gu {names[gu.GroupId]} {memberIndex[gu.OrganizationUserId]}"))
            .Concat(context.Registry.CollectionTargetSizes.Select(s => $"size {s}"))
            .ToList();
    }

    private static void Run(SeederContext context, int count, SeedPresetAccessShape shape, int seed = 1) =>
        new CreateShapedCollectionsStep(count, shape, PermissionDistributions.Enterprise, seed).Execute(context);

    private static SeederContext ContextWithMembers(int memberCount, int inactive = 0)
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        context.Registry.HardenedOrgUserIds.AddRange(NewIds(memberCount));
        context.Registry.InactiveOrgUserIds.AddRange(NewIds(inactive));
        return context;
    }

    private static SeederContext ContextWithGroups(int memberCount, int groupCount, SeedPresetAccessShape shape, int inactive = 0)
    {
        var context = ContextWithMembers(memberCount, inactive);
        new CreateShapedGroupsStep(groupCount, shape, seed: 1).Execute(context);
        return context;
    }

    /// <summary>
    /// Confirmed members each in their own one-member group, so every copied twin brings at least one group.
    /// </summary>
    private static SeederContext ContextWithOneGroupPerMember(int confirmed, int inactive)
    {
        var context = ContextWithMembers(confirmed, inactive);
        var groups = AddRegisteredGroups(context, Enumerable.Repeat(1, confirmed).ToArray());
        for (var i = 0; i < confirmed; i++)
        {
            context.GroupUsers.Add(new GroupUser { GroupId = groups[i], OrganizationUserId = context.Registry.HardenedOrgUserIds[i] });
        }
        return context;
    }

    private static List<Guid> AddRegisteredGroups(SeederContext context, int[] memberCounts)
    {
        var ids = NewIds(memberCounts.Length);
        for (var i = 0; i < ids.Count; i++)
        {
            context.Registry.GroupIds.Add(ids[i]);
            context.Registry.GroupMemberCounts[ids[i]] = memberCounts[i];
        }
        return ids;
    }

    private static List<Guid> NewIds(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    private static Dictionary<Guid, int> CollectionIndex(SeederContext context) =>
        context.Registry.CollectionIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

    private static Dictionary<Guid, int> MemberIndex(SeederContext context) =>
        context.Registry.HardenedOrgUserIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

    private static List<int> GrantedCollections(SeederContext context, Guid groupId)
    {
        var index = CollectionIndex(context);
        return context.CollectionGroups.Where(cg => cg.GroupId == groupId).Select(cg => index[cg.CollectionId]).Order().ToList();
    }
}
