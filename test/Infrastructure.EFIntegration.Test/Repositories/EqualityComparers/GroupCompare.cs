using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class GroupCompare : IEqualityComparer<Group>
{
    public bool Equals(Group x, Group y)
    {
        return x.Name == y.Name &&
        x.AccessAll == y.AccessAll &&
        x.ExternalId == y.ExternalId;
    }

    public int GetHashCode([DisallowNull] Group obj)
    {
        return base.GetHashCode();
    }
}
