namespace Bit.Core.Utilities;

public static class SpanExtensions
{
    public static bool TrySplitBy(
        this ReadOnlySpan<char> input,
        char splitChar,
        out ReadOnlySpan<char> chunk,
        out ReadOnlySpan<char> rest
    )
    {
        var splitIndex = input.IndexOf(splitChar);

        if (splitIndex == -1)
        {
            chunk = default;
            rest = input;
            return false;
        }

        chunk = input[..splitIndex];
        rest = input[++splitIndex..];
        return true;
    }
}
