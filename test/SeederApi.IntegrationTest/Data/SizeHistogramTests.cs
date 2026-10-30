using Bit.Seeder.Data.Distributions;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Data;

public class SizeHistogramTests
{
    [Fact]
    public void Constructor_NoBuckets_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SizeHistogram([]));

        Assert.Contains("at least one bucket", ex.Message);
    }

    [Theory]
    [InlineData(-1, 5, 1.0)]
    [InlineData(6, 5, 1.0)]
    [InlineData(1, 5, -0.5)]
    public void Constructor_InvalidBucket_Throws(int min, int max, double weight)
    {
        var ex = Assert.Throws<ArgumentException>(() => new SizeHistogram([(0, 1, 1.0), (min, max, weight)]));

        Assert.Contains("0 <= min <= max and a non-negative weight", ex.Message);
    }

    [Fact]
    public void Constructor_AllZeroWeights_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SizeHistogram([(1, 5, 0.0), (6, 9, 0.0)]));

        Assert.Contains("positive total weight", ex.Message);
    }

    [Fact]
    public void Draw_ReturnsRequestedCount()
    {
        var histogram = new SizeHistogram([(1, 10, 1.0), (11, 500, 2.0)]);

        Assert.Equal(37, histogram.Draw(37, new Random(1)).Length);
    }

    [Fact]
    public void Draw_EveryValueFallsInsideABucket()
    {
        var histogram = new SizeHistogram([(0, 9, 1.0), (100, 1000, 3.0)]);

        var sizes = histogram.Draw(400, new Random(1));

        Assert.All(sizes, s => Assert.True(s is >= 0 and <= 9 or >= 100 and <= 1000, $"{s} is outside both buckets"));
    }

    [Fact]
    public void Draw_BucketCountsFollowWeightsExactly()
    {
        var histogram = new SizeHistogram([(0, 9, 1.0), (100, 1000, 3.0)]);

        var sizes = histogram.Draw(8, new Random(1));

        Assert.Equal(2, sizes.Count(s => s <= 9));
        Assert.Equal(6, sizes.Count(s => s >= 100));
    }

    [Fact]
    public void Draw_SingleValueBucket_ReturnsThatValue()
    {
        var histogram = new SizeHistogram([(7, 7, 1.0)]);

        Assert.All(histogram.Draw(20, new Random(1)), s => Assert.Equal(7, s));
    }

    [Fact]
    public void Draw_SameSeed_GivesSameSizes()
    {
        var histogram = new SizeHistogram([(0, 9, 1.0), (10, 5000, 1.0)]);

        Assert.Equal(histogram.Draw(50, new Random(9)), histogram.Draw(50, new Random(9)));
    }

    [Fact]
    public void ScaleTo_SmallRoundingDrift_SumsExactlyToTarget()
    {
        // 1 × 10/3 rounds to 3 each (sum 9); the drift step adds the missing 1
        int[] sizes = [1, 1, 1];

        SizeHistogram.ScaleTo(sizes, 10, cap: 100);

        Assert.Equal(10, sizes.Sum());
    }

    [Fact]
    public void ScaleTo_ReachableTarget_SumsExactly()
    {
        // ×26/21 gives [12, 12, 1], capped to [10, 10, 1] (sum 21). Only the last entry can grow, and it needs all 5.
        int[] sizes = [10, 10, 1];

        SizeHistogram.ScaleTo(sizes, 26, cap: 10);

        Assert.Equal(new[] { 10, 10, 6 }, sizes);
    }

    [Fact]
    public void ScaleTo_EntriesThatWouldRoundToZeroOrExceedCap_StayWithinOneToCap()
    {
        // ×60/151: 100 → 40 (cap), 1 → 0 (raised to 1), 50 → 20
        int[] sizes = [100, 1, 50];

        SizeHistogram.ScaleTo(sizes, 60, cap: 40);

        Assert.All(sizes, s => Assert.InRange(s, 1, 40));
    }

    [Fact]
    public void ScaleTo_ZeroEntries_StayZero()
    {
        int[] sizes = [4, 0, 6, 0];

        SizeHistogram.ScaleTo(sizes, 50, cap: 100);

        Assert.Equal(0, sizes[1]);
        Assert.Equal(0, sizes[3]);
    }

    [Theory]
    [InlineData(new[] { 3, 5 }, 0)]
    [InlineData(new[] { 3, 5 }, -5)]
    [InlineData(new[] { 0, 0, 0 }, 10)]
    public void ScaleTo_NoPositiveTargetOrNothingToScale_LeavesArrayUntouched(int[] sizes, int target)
    {
        var before = (int[])sizes.Clone();

        SizeHistogram.ScaleTo(sizes, target, cap: 100);

        Assert.Equal(before, sizes);
    }

    [Fact]
    public void ScaleTo_TargetAboveEntriesTimesCap_StopsWithEveryEntryAtCap()
    {
        int[] sizes = [1, 2, 3];

        SizeHistogram.ScaleTo(sizes, 100, cap: 10);

        Assert.Equal(new[] { 10, 10, 10 }, sizes);
    }

    [Fact]
    public void ScaleTo_TargetBelowEntryCount_StopsWithEveryEntryAtOne()
    {
        int[] sizes = [5, 5, 5];

        SizeHistogram.ScaleTo(sizes, 2, cap: 10);

        Assert.Equal(new[] { 1, 1, 1 }, sizes);
    }

    [Fact]
    public void ParetoWeights_ReturnsRequestedCount()
    {
        Assert.Equal(250, SizeHistogram.ParetoWeights(250, 1.5, new Random(1)).Length);
    }

    [Fact]
    public void ParetoWeights_EveryWeightIsAtLeastOne()
    {
        Assert.All(SizeHistogram.ParetoWeights(10_000, 1.5, new Random(1)), w => Assert.True(w >= 1, $"{w} < 1"));
    }

    [Fact]
    public void ParetoWeights_SameSeed_GivesSameWeights()
    {
        Assert.Equal(
            SizeHistogram.ParetoWeights(100, 1.5, new Random(4)),
            SizeHistogram.ParetoWeights(100, 1.5, new Random(4)));
    }

    [Fact]
    public void ParetoWeights_LowerAlpha_GivesHeavierTail()
    {
        // Same seed, so both draw the same uniforms; (1 - u)^(-1/alpha) grows as alpha falls
        var heavy = SizeHistogram.ParetoWeights(10_000, 1.0, new Random(2));
        var light = SizeHistogram.ParetoWeights(10_000, 3.0, new Random(2));

        Assert.True(heavy.Max() > light.Max(), $"max {heavy.Max()} (alpha 1) <= {light.Max()} (alpha 3)");
    }

    [Fact]
    public void SampleWithoutReplacement_ReturnsDistinctIndices()
    {
        var weights = Enumerable.Repeat(1.0, 20).ToArray();

        var sample = SizeHistogram.SampleWithoutReplacement(weights, 15, new Random(1));

        Assert.Equal(sample.Count, sample.Distinct().Count());
    }

    [Fact]
    public void SampleWithoutReplacement_SkipsZeroWeightAndIneligibleIndices()
    {
        double[] weights = [0, 1, 1, 0, 1, 1];

        var sample = SizeHistogram.SampleWithoutReplacement(weights, 6, new Random(1), u => u != 1);

        Assert.Subset(new HashSet<int> { 2, 4, 5 }, sample.ToHashSet());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(10, 3)]
    public void SampleWithoutReplacement_ReturnsTakeOrEveryEligibleIndexIfFewer(int take, int expected)
    {
        double[] weights = [0, 1, 1, 0, 1, 1];

        var sample = SizeHistogram.SampleWithoutReplacement(weights, take, new Random(1), u => u != 1);

        Assert.Equal(expected, sample.Count);
    }

    [Fact]
    public void SampleWithoutReplacement_HeavyWeight_WinsAlmostEveryDraw()
    {
        // P(index 1 first) = 1000 / 1001; seeds 0-999 give exactly one loss
        var heavyWins = Enumerable.Range(0, 1000)
            .Count(seed => SizeHistogram.SampleWithoutReplacement([1, 1000], 1, new Random(seed))[0] == 1);

        Assert.Equal(999, heavyWins);
    }
}
