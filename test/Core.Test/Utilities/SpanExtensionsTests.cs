using Bit.Core.Utilities;
using Xunit;

#nullable enable

namespace Bit.Core.Test.Utilities;

public class SpanExtensionsTests
{
    [Theory]
    [InlineData(".", "", "")]
    [InlineData("T.T", "T", "T")]
    [InlineData("T.", "T", "")]
    [InlineData(".T", "", "T")]
    [InlineData("T.T.T", "T", "T.T")]
    public void TrySplitBy_CanSplit_Success(string fullString, string firstPart, string secondPart)
    {
        var success = fullString.AsSpan().TrySplitBy('.', out var firstPartSpan, out var secondPartSpan);
        Assert.True(success);
        Assert.Equal(firstPart, firstPartSpan.ToString());
        Assert.Equal(secondPart, secondPartSpan.ToString());
    }

    [Theory]
    [InlineData("Test", '.')]
    [InlineData("Other test", 'S')]
    public void TrySplitBy_CanNotSplit_Success(string fullString, char splitChar)
    {
        var success = fullString.AsSpan().TrySplitBy(splitChar, out var splitChunk, out var rest);
        Assert.False(success);
        Assert.True(splitChunk.IsEmpty);
        Assert.Equal(fullString, rest.ToString());
    }

    [Theory]
    [InlineData("11111", '1', 5)]
    [InlineData("Text", 'z', 0)]
    [InlineData("1", '1', 1)]
    public void Count_ReturnsCount(string text, char countChar, int expectedInstances)
    {
        Assert.Equal(expectedInstances, text.AsSpan().Count(countChar));
    }

    [Theory]
    [InlineData(new[] { 5, 4 }, 5, 1)]
    [InlineData(new[] { 1 }, 5, 0)]
    [InlineData(new[] { 5, 5, 5 }, 5, 3)]
    public void CountIntegers_ReturnsCount(int[] array, int countNumber, int expectedInstances)
    {
        Assert.Equal(expectedInstances, ((ReadOnlySpan<int>)array.AsSpan()).Count(countNumber));
    }
}
