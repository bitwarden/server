using System.Diagnostics.CodeAnalysis;

namespace Bit.Infrastructure.IntegrationTest.Comparers;

/// <summary>
/// A datetime comparer that doesn't care about overall ticks and instead only cares about Second precision.
/// </summary>
public class LaxDateTimeComparer : IEqualityComparer<DateTime>
{
    public static readonly IEqualityComparer<DateTime> Default = new LaxDateTimeComparer();

    public bool Equals(DateTime x, DateTime y)
    {
        return x.Date == y.Date
            && x.Hour == y.Hour
            && x.Minute == y.Minute
            && x.Second == y.Second;
    }
    public int GetHashCode([DisallowNull] DateTime obj)
    {
        return HashCode.Combine(obj.Date, obj.Hour, obj.Minute, obj.Second);
    }
}
