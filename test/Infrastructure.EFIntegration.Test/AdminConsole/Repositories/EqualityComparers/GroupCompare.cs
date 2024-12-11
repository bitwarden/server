using System.Diagnostics.CodeAnalysis;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.AdminConsole.Repositories.EqualityComparers;

public class GroupCompare : IEqualityComparer<Group>
{
    public bool Equals(Group x, Group y)
    {
        return x.Name == y.Name && x.ExternalId == y.ExternalId;
    }

    public int GetHashCode([DisallowNull] Group obj)
    {
        return base.GetHashCode();
    }
}
