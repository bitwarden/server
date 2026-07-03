using Bit.Seeder.Data.Distributions;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.DensityModel;

/// <summary>
/// Validates the archived/deleted/"both" target math (shared by GeneratePersonalCiphersStep and
/// GenerateCiphersStep) and the index-selection/round-robin-attribution behavior in
/// <see cref="ArchiveDeleteDistribution"/> — the actual production selection algorithm, not a
/// reimplementation of it.
/// </summary>
public sealed class ArchiveAndDeleteRateTests
{
    [Fact]
    public void ArchivedTarget_RespectsRate_BeforeCeiling()
    {
        var (archivedTarget, _, _, _) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 500, archivedRate: 0.06, deletedRate: 0, overlapRate: 0, maxArchived: 50, maxDeleted: 25);

        Assert.Equal(30, archivedTarget);
    }

    [Fact]
    public void ArchivedTarget_CeilingWins_WhenRateExceedsCap()
    {
        var (archivedTarget, _, _, _) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 10_000, archivedRate: 0.06, deletedRate: 0, overlapRate: 0, maxArchived: 50, maxDeleted: 25);

        Assert.Equal(50, archivedTarget);
    }

    [Fact]
    public void DeletedTarget_CeilingWins_WhenRateExceedsCap()
    {
        var (_, deletedTarget, _, _) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 10_000, archivedRate: 0, deletedRate: 0.035, overlapRate: 0, maxArchived: 50, maxDeleted: 25);

        Assert.Equal(25, deletedTarget);
    }

    /// <summary>
    /// Regression test: bothTarget must clamp against MaxDeletedCiphers, not just archivedTarget.
    /// </summary>
    [Fact]
    public void BothTarget_NeverExceedsMaxDeletedCiphers_EvenWhenArchivedCeilingIsHigher()
    {
        var (archivedTarget, deletedTarget, bothTarget, deletedOnlyTarget) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 10_000, archivedRate: 0.06, deletedRate: 0.035, overlapRate: 0.02, maxArchived: 50, maxDeleted: 25);

        Assert.Equal(50, archivedTarget);
        Assert.Equal(25, deletedTarget);
        Assert.Equal(25, bothTarget);
        Assert.Equal(0, deletedOnlyTarget);

        var totalDeleted = bothTarget + deletedOnlyTarget;
        Assert.Equal(25, totalDeleted);
        Assert.True(totalDeleted <= 25, $"Total deleted ({totalDeleted}) must not exceed MaxDeletedCiphers (25).");
    }

    [Fact]
    public void BothTarget_NeverExceedsArchivedTarget()
    {
        // Overlap rate alone would compute 100, but archivedTarget caps it at 40.
        var (archivedTarget, _, bothTarget, _) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 2_000, archivedRate: 0.02, deletedRate: 0, overlapRate: 0.05, maxArchived: 50, maxDeleted: 25);

        Assert.Equal(40, archivedTarget);
        Assert.True(bothTarget <= archivedTarget);
    }

    [Fact]
    public void ZeroRates_ProduceZeroTargets()
    {
        var (archivedTarget, deletedTarget, bothTarget, deletedOnlyTarget) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 10_000, archivedRate: 0, deletedRate: 0, overlapRate: 0, maxArchived: 50, maxDeleted: 25);

        Assert.Equal(0, archivedTarget);
        Assert.Equal(0, deletedTarget);
        Assert.Equal(0, bothTarget);
        Assert.Equal(0, deletedOnlyTarget);
    }

    [Fact]
    public void DeletedOrgTarget_ComputedIndependently_FromOwnPoolAndOwnCeiling()
    {
        // Mirrors GenerateCiphersStep's org-only delete sizing: independent of the personal pool.
        const int orgCipherCount = 10_000;
        const double deletedRate = 0.035;
        const int maxDeleted = 25;

        var deletedOrgTarget = deletedRate > 0
            ? Math.Min((int)(orgCipherCount * deletedRate), maxDeleted)
            : 0;

        Assert.Equal(25, deletedOrgTarget);

        var selection = ArchiveDeleteDistribution.Select(orgCipherCount, archivedTarget: 0, bothTarget: 0, deletedOnlyTarget: deletedOrgTarget);

        Assert.Equal(maxDeleted, selection.DeletedOnly.Count);
    }

    [Fact]
    public void OrgPool_ArchivedTarget_ComputedIndependently_FromPersonalPoolAndOwnCeiling()
    {
        // GenerateCiphersStep now archives org ciphers too (for a round-robin-selected org member,
        // per DensityProfile.ArchivedCipherRate's doc comment) using this same formula against the
        // org-cipher count instead of the personal expectedTotal, with its own independent cap.
        var (archivedTarget, deletedTarget, bothTarget, deletedOnlyTarget) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 5_000, archivedRate: 0.06, deletedRate: 0.03, overlapRate: 0.02, maxArchived: 50, maxDeleted: 25);

        Assert.Equal(50, archivedTarget);
        Assert.Equal(25, deletedTarget);
        Assert.Equal(25, bothTarget);
        Assert.Equal(0, deletedOnlyTarget);
    }

    [Theory]
    [InlineData(0, 0.03, 0)]   // archived rate 0 while delete rate is set
    [InlineData(0.06, 0, 0)]  // delete rate 0 while archived rate is set
    [InlineData(0.06, 0.03, 0)] // overlap rate 0 while the other two are set
    public void Select_GuardsAgainstZeroTarget_NoException(double archivedRate, double deletedRate, double overlapRate)
    {
        var (archivedTarget, _, bothTarget, deletedOnlyTarget) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 1_000, archivedRate: archivedRate, deletedRate: deletedRate, overlapRate: overlapRate,
            maxArchived: 50, maxDeleted: 25);

        // The point of this test: this call must not throw.
        ArchiveDeleteDistribution.Select(1_000, archivedTarget, bothTarget, deletedOnlyTarget);
    }

    [Fact]
    public void Select_IsBoth_ImpliesIsArchived_ForAllSelectedIndices()
    {
        const int total = 10_000;
        var (archivedTarget, _, bothTarget, _) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: total, archivedRate: 0.06, deletedRate: 0.035, overlapRate: 0.02, maxArchived: 50, maxDeleted: 25);

        var selection = ArchiveDeleteDistribution.Select(total, archivedTarget, bothTarget, deletedOnlyTarget: 0);

        Assert.True(selection.Both.IsSubsetOf(selection.Archived));
    }

    [Fact]
    public void Select_DeletedOnly_NeverOverlapsArchived()
    {
        const int total = 10_000;
        var (archivedTarget, _, bothTarget, deletedOnlyTarget) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: total, archivedRate: 0.06, deletedRate: 0.035, overlapRate: 0.02, maxArchived: 50, maxDeleted: 25);

        var selection = ArchiveDeleteDistribution.Select(total, archivedTarget, bothTarget, deletedOnlyTarget);

        Assert.Empty(selection.Archived.Intersect(selection.DeletedOnly));
    }

    /// <summary>
    /// Regression test: the old stride formula overshot the target when poolSize wasn't an exact
    /// multiple of it.
    /// </summary>
    [Fact]
    public void Select_ProducesExactArchivedCount_EvenWhenPoolSizeDoesNotDivideEvenly()
    {
        var selection = ArchiveDeleteDistribution.Select(poolSize: 2_909, archivedTarget: 50, bothTarget: 25, deletedOnlyTarget: 0);

        Assert.Equal(50, selection.Archived.Count);
        Assert.Equal(25, selection.Both.Count);
    }

    /// <summary>
    /// Regression test: the old stride algorithm could starve deleted-only ciphers when their
    /// candidate indices collided with already-claimed archived indices.
    /// </summary>
    [Fact]
    public void Select_ProducesExactDeletedOnlyCount_EvenWhenItWouldCollideWithArchivedStride()
    {
        var selection = ArchiveDeleteDistribution.Select(poolSize: 200, archivedTarget: 8, bothTarget: 2, deletedOnlyTarget: 2);

        Assert.Equal(8, selection.Archived.Count);
        Assert.Equal(2, selection.Both.Count);
        Assert.Equal(2, selection.DeletedOnly.Count);
        Assert.Empty(selection.Archived.Intersect(selection.DeletedOnly));
    }

    /// <summary>
    /// Regression test: ArchivedOrder[0] must never land in Both when there's an archive-only
    /// position available — otherwise a client that hides deleted items from Archive would show
    /// nothing for whichever user is pointed at it.
    /// </summary>
    [Fact]
    public void Select_ArchivedOrderFirst_IsNeverBoth_WhenNotEveryArchivedCipherIsAlsoDeleted()
    {
        var selection = ArchiveDeleteDistribution.Select(poolSize: 2_909, archivedTarget: 50, bothTarget: 25, deletedOnlyTarget: 0);

        Assert.DoesNotContain(selection.ArchivedOrder[0], selection.Both);
    }

    [Fact]
    public void Select_ArchivedOrderFirst_CanBeBoth_WhenEveryArchivedCipherIsAlsoDeleted()
    {
        // No archive-only ciphers exist at all in this configuration — there's nothing to reserve.
        var selection = ArchiveDeleteDistribution.Select(poolSize: 1_000, archivedTarget: 25, bothTarget: 25, deletedOnlyTarget: 0);

        Assert.Contains(selection.ArchivedOrder[0], selection.Both);
        Assert.Equal(25, selection.Both.Count);
    }

    [Fact]
    public void EvenlySpacedIndices_ReturnsDistinctAscendingIndices()
    {
        var indices = ArchiveDeleteDistribution.EvenlySpacedIndices(poolSize: 17, target: 5);

        Assert.Equal(5, indices.Count);
        Assert.Equal(indices.Distinct().Count(), indices.Count);
        Assert.Equal(indices.OrderBy(i => i), indices);
        Assert.All(indices, i => Assert.InRange(i, 0, 16));
    }

    [Fact]
    public void EvenlySpacedIndices_ClampsTarget_WhenTargetExceedsPoolSize()
    {
        var indices = ArchiveDeleteDistribution.EvenlySpacedIndices(poolSize: 5, target: 50);

        Assert.Equal(5, indices.Count);
        Assert.Equal([0, 1, 2, 3, 4], indices);
    }

    /// <summary>
    /// Regression test: index-based modulo attribution collapses to gcd(stride, userCount) distinct
    /// users; assignment-count attribution must spread evenly regardless.
    /// </summary>
    [Fact]
    public void AssignRoundRobinUserPositions_SpreadsAcrossAllUsers_RegardlessOfGcdWithUserCount()
    {
        var selection = ArchiveDeleteDistribution.Select(poolSize: 5_000, archivedTarget: 50, bothTarget: 25, deletedOnlyTarget: 0);

        var positions = ArchiveDeleteDistribution.AssignRoundRobinUserPositions(selection.ArchivedOrder, userCount: 250);

        Assert.Equal(50, positions.Values.Distinct().Count());
    }

    [Fact]
    public void AssignRoundRobinUserPositions_FirstArchivedCipher_AlwaysGoesToPositionZero()
    {
        // CreateOwnerStep always adds the Owner to UserDigests first, so userDigests[0] is the
        // Owner — position 0 in this map is therefore always the Owner's slot.
        var selection = ArchiveDeleteDistribution.Select(poolSize: 200, archivedTarget: 8, bothTarget: 2, deletedOnlyTarget: 0);

        var positions = ArchiveDeleteDistribution.AssignRoundRobinUserPositions(selection.ArchivedOrder, userCount: 7);

        Assert.Equal(0, positions[selection.ArchivedOrder[0]]);
    }

    [Fact]
    public void AssignRoundRobinUserPositions_WrapsAround_WhenMoreArchivedThanUsers()
    {
        var selection = ArchiveDeleteDistribution.Select(poolSize: 100, archivedTarget: 10, bothTarget: 0, deletedOnlyTarget: 0);

        var positions = ArchiveDeleteDistribution.AssignRoundRobinUserPositions(selection.ArchivedOrder, userCount: 3);

        Assert.All(positions.Values, p => Assert.InRange(p, 0, 2));
        Assert.Equal(3, positions.Values.Distinct().Count());
    }

    [Fact]
    public void ComputeTargets_CanArchiveFalse_ZeroesArchivedAndBothTargets_ButNotDeletedOnlyTarget()
    {
        var (archivedTarget, deletedTarget, bothTarget, deletedOnlyTarget) = ArchiveDeleteDistribution.ComputeTargets(
            poolSize: 10_000, archivedRate: 0.06, deletedRate: 0.035, overlapRate: 0.02, maxArchived: 50, maxDeleted: 25,
            canArchive: false);

        Assert.Equal(0, archivedTarget);
        Assert.Equal(0, bothTarget);
        Assert.Equal(25, deletedTarget);
        Assert.Equal(25, deletedOnlyTarget);
    }
}
