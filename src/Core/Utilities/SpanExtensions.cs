namespace Bit.Core.Utilities;

public static class SpanExtensions
{
    public static bool TrySplitBy(this ReadOnlySpan<char> input,
        char splitChar, out ReadOnlySpan<char> chunk, out ReadOnlySpan<char> rest)
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

    // Replace with the implementation from .NET 8 when we upgrade
    // Ref: https://github.com/dotnet/runtime/issues/59466
    public static int Count<T>(this ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>
    {
        var count = 0;
        int pos;

        while ((pos = span.IndexOf(value)) >= 0)
        {
            span = span[++pos..];
            count++;
        }

        return count;
    }
}
