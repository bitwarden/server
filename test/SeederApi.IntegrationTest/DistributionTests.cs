using Bit.Seeder.Data.Distributions;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class DistributionTests
{
    [Fact]
    public void Constructor_PercentagesSumToOne_Succeeds()
    {
        var distribution = new Distribution<string>(
            ("A", 0.50),
            ("B", 0.30),
            ("C", 0.20)
        );

        Assert.NotNull(distribution);
    }

    [Fact]
    public void Constructor_PercentagesDoNotSumToOne_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Distribution<int>(
            (1, 0.50),
            (2, 0.40)
        ));

        Assert.Contains("must sum to 1.0", exception.Message);
    }

    [Fact]
    public void Constructor_PercentagesExceedOne_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Distribution<string>(
            ("X", 0.60),
            ("Y", 0.60)
        ));

        Assert.Contains("must sum to 1.0", exception.Message);
    }

    [Fact]
    public void Constructor_WithinToleranceOf001_Succeeds()
    {
        var distribution = new Distribution<string>(
            ("A", 0.333),
            ("B", 0.333),
            ("C", 0.333)
        );

        Assert.NotNull(distribution);
    }

    [Fact]
    public void Select_ReturnsCorrectBuckets_ForEvenSplit()
    {
        var distribution = new Distribution<string>(
            ("A", 0.50),
            ("B", 0.50)
        );

        Assert.Equal("A", distribution.Select(0, 100));
        Assert.Equal("A", distribution.Select(49, 100));
        Assert.Equal("B", distribution.Select(50, 100));
        Assert.Equal("B", distribution.Select(99, 100));
    }

    [Fact]
    public void Select_ReturnsCorrectBuckets_ForThreeWaySplit()
    {
        var distribution = new Distribution<int>(
            (1, 0.60),
            (2, 0.30),
            (3, 0.10)
        );

        Assert.Equal(1, distribution.Select(0, 100));
        Assert.Equal(1, distribution.Select(59, 100));
        Assert.Equal(2, distribution.Select(60, 100));
        Assert.Equal(2, distribution.Select(89, 100));
        Assert.Equal(3, distribution.Select(90, 100));
        Assert.Equal(3, distribution.Select(99, 100));
    }

    [Fact]
    public void Select_IndexBeyondTotal_ReturnsLastBucket()
    {
        var distribution = new Distribution<string>(
            ("A", 0.50),
            ("B", 0.50)
        );

        Assert.Equal("B", distribution.Select(150, 100));
    }

    [Fact]
    public void Select_SmallTotal_HandlesRoundingGracefully()
    {
        var distribution = new Distribution<string>(
            ("A", 0.33),
            ("B", 0.33),
            ("C", 0.34)
        );

        Assert.Equal("A", distribution.Select(0, 10));
        Assert.Equal("A", distribution.Select(2, 10));
        Assert.Equal("B", distribution.Select(3, 10));
        Assert.Equal("C", distribution.Select(9, 10));
    }

    [Fact]
    public void GetCounts_ReturnsCorrectCounts_ForEvenSplit()
    {
        var distribution = new Distribution<string>(
            ("X", 0.50),
            ("Y", 0.50)
        );

        var counts = distribution.GetCounts(100).ToList();

        Assert.Equal(2, counts.Count);
        Assert.Equal(("X", 50), counts[0]);
        Assert.Equal(("Y", 50), counts[1]);
    }

    [Fact]
    public void GetCounts_LastBucketReceivesRemainder()
    {
        var distribution = new Distribution<string>(
            ("A", 0.33),
            ("B", 0.33),
            ("C", 0.34)
        );

        var counts = distribution.GetCounts(100).ToList();

        Assert.Equal(3, counts.Count);
        Assert.Equal(33, counts[0].Count);
        Assert.Equal(33, counts[1].Count);
        Assert.Equal(34, counts[2].Count);
    }

    [Fact]
    public void GetCounts_TotalCountsMatchInput()
    {
        var distribution = new Distribution<int>(
            (1, 0.25),
            (2, 0.25),
            (3, 0.25),
            (4, 0.25)
        );

        var counts = distribution.GetCounts(1000).ToList();
        var total = counts.Sum(c => c.Count);

        Assert.Equal(1000, total);
    }

    [Fact]
    public void Select_IsDeterministic_SameInputSameOutput()
    {
        var distribution = new Distribution<string>(
            ("Alpha", 0.40),
            ("Beta", 0.35),
            ("Gamma", 0.25)
        );

        for (int i = 0; i < 100; i++)
        {
            var first = distribution.Select(i, 100);
            var second = distribution.Select(i, 100);
            Assert.Equal(first, second);
        }
    }
}
