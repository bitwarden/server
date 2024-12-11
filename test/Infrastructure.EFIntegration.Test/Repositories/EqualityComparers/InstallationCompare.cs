using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class InstallationCompare : IEqualityComparer<Installation>
{
    public bool Equals(Installation x, Installation y)
    {
        return x.Email == y.Email && x.Key == y.Key && x.Enabled == y.Enabled;
    }

    public int GetHashCode([DisallowNull] Installation obj)
    {
        return base.GetHashCode();
    }
}
