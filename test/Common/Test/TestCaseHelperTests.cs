using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Test.Common.Test;

public class TestCaseHelperTests
{
    [Fact]
    public void GetCombinations_EmptyList()
    {
        Assert.Equal(new[] { Array.Empty<int>() }, TestCaseHelper.GetCombinations(Array.Empty<int>()).ToArray());
    }

    [Fact]
    public void GetCombinations_OneItemList()
    {
        Assert.Equal(new[] { Array.Empty<int>(), new[] { 1 } }, TestCaseHelper.GetCombinations(1));
    }

    [Fact]
    public void GetCombinations_TwoItemList()
    {
        Assert.Equal(new[] { Array.Empty<int>(), new[] { 2 }, new[] { 1 }, new[] { 1, 2 } }, TestCaseHelper.GetCombinations(1, 2));
    }

    [Fact]
    public void GetCombinationsOfMultipleLists_OneOne()
    {
        Assert.Equal(new[] { new object[] { 1, "1" } }, TestCaseHelper.GetCombinationsOfMultipleLists(new object[] { 1 }, new object[] { "1" }));
    }


    [Fact]
    public void GetCombinationsOfMultipleLists_OneTwo()
    {
        Assert.Equal(new[] { new object[] { 1, "1" }, new object[] { 1, "2" } }, TestCaseHelper.GetCombinationsOfMultipleLists(new object[] { 1 }, new object[] { "1", "2" }));
    }

    [Fact]
    public void GetCombinationsOfMultipleLists_TwoOne()
    {
        Assert.Equal(new[] { new object[] { 1, "1" }, new object[] { 2, "1" } }, TestCaseHelper.GetCombinationsOfMultipleLists(new object[] { 1, 2 }, new object[] { "1" }));
    }

    [Fact]
    public void GetCombinationsOfMultipleLists_TwoTwo()
    {
        Assert.Equal(new[] { new object[] { 1, "1" }, new object[] { 1, "2" }, new object[] { 2, "1" }, new object[] { 2, "2" } }, TestCaseHelper.GetCombinationsOfMultipleLists(new object[] { 1, 2 }, new object[] { "1", "2" }));
    }
}
