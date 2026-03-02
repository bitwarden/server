using Bit.Core.Entities;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.DensityModel;

public class CreateCollectionsStepTests
{
    private static readonly List<Guid> _collectionIds =
        [.. Enumerable.Range(1, 10).Select(i => new Guid($"00000000-0000-0000-0000-{i:D12}"))];

    private static readonly List<Guid> _groupIds =
        [.. Enumerable.Range(1, 5).Select(i => new Guid($"11111111-0000-0000-0000-{i:D12}"))];

    private static readonly List<Guid> _userIds =
        [.. Enumerable.Range(1, 20).Select(i => new Guid($"22222222-0000-0000-0000-{i:D12}"))];

    private static readonly Distribution<PermissionWeight> EvenPermissions = new(
        (PermissionWeight.ReadOnly, 0.25),
        (PermissionWeight.ReadWrite, 0.25),
        (PermissionWeight.Manage, 0.25),
        (PermissionWeight.HidePasswords, 0.25));

    [Fact]
    public void ApplyGroupPermissions_EvenSplit_DistributesAllFourTypes()
    {
        var assignments = Enumerable.Range(0, 100)
            .Select(_ => new CollectionGroup { CollectionId = Guid.NewGuid(), GroupId = Guid.NewGuid() })
            .ToList();

        CreateCollectionsStep.ApplyGroupPermissions(assignments, EvenPermissions);

        Assert.Equal(25, assignments.Count(a => a.ReadOnly));
        Assert.Equal(25, assignments.Count(a => a.Manage));
        Assert.Equal(25, assignments.Count(a => a.HidePasswords));
        Assert.Equal(25, assignments.Count(a => !a.ReadOnly && !a.Manage && !a.HidePasswords));
    }

    [Fact]
    public void ApplyGroupPermissions_MutuallyExclusiveFlags()
    {
        var assignments = Enumerable.Range(0, 100)
            .Select(_ => new CollectionGroup { CollectionId = Guid.NewGuid(), GroupId = Guid.NewGuid() })
            .ToList();

        CreateCollectionsStep.ApplyGroupPermissions(assignments, EvenPermissions);

        Assert.All(assignments, a =>
        {
            var flagCount = (a.ReadOnly ? 1 : 0) + (a.HidePasswords ? 1 : 0) + (a.Manage ? 1 : 0);
            Assert.True(flagCount <= 1, "At most one permission flag should be true");
        });
    }

    [Fact]
    public void ApplyGroupPermissions_ReadOnlyHeavy_MajorityAreReadOnly()
    {
        var assignments = Enumerable.Range(0, 100)
            .Select(_ => new CollectionGroup { CollectionId = Guid.NewGuid(), GroupId = Guid.NewGuid() })
            .ToList();

        CreateCollectionsStep.ApplyGroupPermissions(assignments, PermissionDistributions.Enterprise);

        var readOnlyCount = assignments.Count(a => a.ReadOnly);
        Assert.True(readOnlyCount >= 80, $"Expected >= 80 ReadOnly, got {readOnlyCount}");
    }

    [Fact]
    public void ApplyUserPermissions_EvenSplit_DistributesAllFourTypes()
    {
        var assignments = Enumerable.Range(0, 100)
            .Select(_ => new CollectionUser { CollectionId = Guid.NewGuid(), OrganizationUserId = Guid.NewGuid() })
            .ToList();

        CreateCollectionsStep.ApplyUserPermissions(assignments, EvenPermissions);

        Assert.Equal(25, assignments.Count(a => a.ReadOnly));
        Assert.Equal(25, assignments.Count(a => a.Manage));
        Assert.Equal(25, assignments.Count(a => a.HidePasswords));
        Assert.Equal(25, assignments.Count(a => !a.ReadOnly && !a.Manage && !a.HidePasswords));
    }

    [Fact]
    public void BuildCollectionGroups_ClampsToAvailableGroups()
    {
        var twoGroups = _groupIds.Take(2).ToList();
        var step = CreateStep(CollectionFanOutShape.Uniform, min: 5, max: 5);

        var result = step.BuildCollectionGroups(_collectionIds, twoGroups);

        Assert.All(result, cg => Assert.Contains(cg.GroupId, twoGroups));
        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void BuildCollectionGroups_NoDuplicateGroupPerCollection()
    {
        var step = CreateStep(CollectionFanOutShape.Uniform, min: 3, max: 3);

        var result = step.BuildCollectionGroups(_collectionIds, _groupIds);

        foreach (var collectionId in _collectionIds)
        {
            var groupsForCollection = result.Where(cg => cg.CollectionId == collectionId)
                .Select(cg => cg.GroupId).ToList();
            Assert.Equal(groupsForCollection.Count, groupsForCollection.Distinct().Count());
        }
    }

    [Fact]
    public void BuildCollectionGroups_Uniform_AssignsGroupsToEveryCollection()
    {
        var step = CreateStep(CollectionFanOutShape.Uniform, min: 2, max: 2);

        var result = step.BuildCollectionGroups(_collectionIds, _groupIds);

        Assert.Equal(20, result.Count);
        Assert.All(result, cg => Assert.Contains(cg.GroupId, _groupIds));
    }

    [Fact]
    public void BuildCollectionUsers_AllCollectionIdsAreValid()
    {
        var result = CreateCollectionsStep.BuildCollectionUsers(_collectionIds, _userIds, 10);

        Assert.All(result, cu => Assert.Contains(cu.CollectionId, _collectionIds));
    }

    [Fact]
    public void BuildCollectionUsers_AssignsOneToThreeCollectionsPerUser()
    {
        var result = CreateCollectionsStep.BuildCollectionUsers(_collectionIds, _userIds, 10);

        var perUser = result.GroupBy(cu => cu.OrganizationUserId).ToList();
        Assert.All(perUser, group => Assert.InRange(group.Count(), 1, 3));
    }

    [Fact]
    public void BuildCollectionUsers_RespectsDirectUserCount()
    {
        var result = CreateCollectionsStep.BuildCollectionUsers(_collectionIds, _userIds, 5);

        var distinctUsers = result.Select(cu => cu.OrganizationUserId).Distinct().ToList();
        Assert.Equal(5, distinctUsers.Count);
    }

    [Fact]
    public void ComputeFanOut_FrontLoaded_FirstTenPercentGetMax()
    {
        var step = CreateStep(CollectionFanOutShape.FrontLoaded, min: 1, max: 5);

        Assert.Equal(5, step.ComputeFanOut(0, 100, 1, 5));
        Assert.Equal(5, step.ComputeFanOut(9, 100, 1, 5));
        Assert.Equal(1, step.ComputeFanOut(10, 100, 1, 5));
        Assert.Equal(1, step.ComputeFanOut(99, 100, 1, 5));
    }

    [Fact]
    public void ComputeFanOut_MinEqualsMax_AlwaysReturnsMin()
    {
        var step = CreateStep(CollectionFanOutShape.Uniform, min: 3, max: 3);

        Assert.Equal(3, step.ComputeFanOut(0, 10, 3, 3));
        Assert.Equal(3, step.ComputeFanOut(5, 10, 3, 3));
        Assert.Equal(3, step.ComputeFanOut(9, 10, 3, 3));
    }

    [Fact]
    public void ComputeFanOut_PowerLaw_FirstCollectionGetsMax()
    {
        var step = CreateStep(CollectionFanOutShape.PowerLaw, min: 1, max: 5);

        Assert.Equal(5, step.ComputeFanOut(0, 100, 1, 5));
    }

    [Fact]
    public void ComputeFanOut_PowerLaw_LaterCollectionsDecay()
    {
        var step = CreateStep(CollectionFanOutShape.PowerLaw, min: 1, max: 5);

        var first = step.ComputeFanOut(0, 100, 1, 5);
        var middle = step.ComputeFanOut(50, 100, 1, 5);
        var last = step.ComputeFanOut(99, 100, 1, 5);

        Assert.True(first > middle, "First collection should have more fan-out than middle");
        Assert.True(middle >= last, "Middle should have >= fan-out than last");
        Assert.True(last >= 1, "Last collection should have at least min fan-out");
    }

    [Fact]
    public void ComputeFanOut_Uniform_CyclesThroughRange()
    {
        var step = CreateStep(CollectionFanOutShape.Uniform, min: 1, max: 3);

        Assert.Equal(1, step.ComputeFanOut(0, 10, 1, 3));
        Assert.Equal(2, step.ComputeFanOut(1, 10, 1, 3));
        Assert.Equal(3, step.ComputeFanOut(2, 10, 1, 3));
        Assert.Equal(1, step.ComputeFanOut(3, 10, 1, 3));
    }

    private static CreateCollectionsStep CreateStep(CollectionFanOutShape shape, int min, int max)
    {
        var density = new DensityProfile
        {
            FanOutShape = shape,
            CollectionFanOutMin = min,
            CollectionFanOutMax = max
        };
        return CreateCollectionsStep.FromCount(0, density);
    }
}
