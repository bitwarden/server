using System.Diagnostics.CodeAnalysis;

namespace Bit.Infrastructure.IntegrationTest.Comparers;

/// <summary>
/// A datetime comparer that doesn't care about overall ticks and instead allows a configurable allowed difference.
/// </summary>
public class LaxDateTimeComparer : IEqualityComparer<DateTime>
{
    public static readonly IEqualityComparer<DateTime> Default = new LaxDateTimeComparer(
        TimeSpan.FromMilliseconds(2)
    );
    private readonly TimeSpan _allowedDifference;

    public LaxDateTimeComparer(TimeSpan allowedDifference)
    {
        _allowedDifference = allowedDifference;
    }

    public bool Equals(DateTime x, DateTime y)
    {
        var difference = x - y;
        return difference.Duration() < _allowedDifference;
    }

    public int GetHashCode([DisallowNull] DateTime obj)
    {
        // Not used when used for Assert.Equal() overload
        throw new NotImplementedException();
    }

    public static int RoundMilliseconds(int milliseconds)
    {
        return (int)Math.Round(milliseconds / 100d) * 100;
    }
}
