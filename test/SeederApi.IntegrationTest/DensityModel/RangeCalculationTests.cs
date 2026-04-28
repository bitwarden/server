using Xunit;

namespace Bit.SeederApi.IntegrationTest.DensityModel;

/// <summary>
/// Validates the range calculation formula used in GeneratePersonalCiphersStep and GenerateFoldersStep.
/// The formula: range.Min + (index % Math.Max(range.Max - range.Min + 1, 1))
/// </summary>
public class RangeCalculationTests
{
    private static int ComputeFromRange(int min, int max, int index)
    {
        return min + (index % Math.Max(max - min + 1, 1));
    }

    [Fact]
    public void RangeFormula_SmallRange_ProducesBothMinAndMax()
    {
        var values = Enumerable.Range(0, 100).Select(i => ComputeFromRange(0, 1, i)).ToHashSet();

        Assert.Contains(0, values);
        Assert.Contains(1, values);
    }

    [Fact]
    public void RangeFormula_LargerRange_MaxIsReachable()
    {
        var values = Enumerable.Range(0, 1000).Select(i => ComputeFromRange(5, 15, i)).ToHashSet();

        Assert.Contains(5, values);
        Assert.Contains(15, values);
        Assert.Equal(11, values.Count); // 5,6,7,...,15
    }

    [Fact]
    public void RangeFormula_SingleValue_AlwaysReturnsMin()
    {
        var values = Enumerable.Range(0, 50).Select(i => ComputeFromRange(3, 3, i)).Distinct().ToList();

        Assert.Single(values);
        Assert.Equal(3, values[0]);
    }

    [Fact]
    public void RangeFormula_AllValuesInBounds()
    {
        for (var i = 0; i < 500; i++)
        {
            var result = ComputeFromRange(50, 200, i);
            Assert.InRange(result, 50, 200);
        }
    }
}
