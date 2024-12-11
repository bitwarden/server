using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class EventCompare : IEqualityComparer<Event>
{
    public bool Equals(Event x, Event y)
    {
        return x.Date.ToShortDateString() == y.Date.ToShortDateString()
            && x.Type == y.Type
            && x.IpAddress == y.IpAddress;
    }

    public int GetHashCode([DisallowNull] Event obj)
    {
        return base.GetHashCode();
    }
}
