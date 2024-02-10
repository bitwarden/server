using System.Buffers;
using Bit.Core.Utilities;
using Xunit;

#nullable enable

namespace Bit.Core.Test.Utilities;

public class SpanExtensionsTests
{
    private static readonly SearchValues<char> _periodChar = SearchValues.Create(".");

    [Theory]
    [InlineData(".", "", "")]
    [InlineData("T.T", "T", "T")]
    [InlineData("T.", "T", "")]
    [InlineData(".T", "", "T")]
    [InlineData("T.T.T", "T", "T.T")]
    public void TrySplitBy_CanSplit_Success(string fullString, string firstPart, string secondPart)
    {
        var success = fullString.AsSpan().TrySplitBy(_periodChar, out var firstPartSpan, out var secondPartSpan);
        Assert.True(success);
        Assert.Equal(firstPart, firstPartSpan.ToString());
        Assert.Equal(secondPart, secondPartSpan.ToString());
    }

    [Theory]
    [InlineData("Test", ".")]
    [InlineData("Other test", "S")]
    public void TrySplitBy_CanNotSplit_Success(string fullString, string splitChar)
    {
        var success = fullString.AsSpan().TrySplitBy(SearchValues.Create(splitChar), out var splitChunk, out var rest);
        Assert.False(success);
        Assert.True(splitChunk.IsEmpty);
        Assert.Equal(fullString, rest.ToString());
    }
}
