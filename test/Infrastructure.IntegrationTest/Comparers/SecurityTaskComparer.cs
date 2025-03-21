using System.Diagnostics.CodeAnalysis;
using Bit.Core.Vault.Entities;

namespace Bit.Infrastructure.IntegrationTest.Comparers;

/// <summary>
/// Determines the equality of two SecurityTask objects.
/// </summary>
public class SecurityTaskComparer : IEqualityComparer<SecurityTask>
{
    public bool Equals(SecurityTask x, SecurityTask y)
    {
        return x.Id.Equals(y.Id) &&
               x.Type.Equals(y.Type) &&
               x.Status.Equals(y.Status);
    }

    public int GetHashCode([DisallowNull] SecurityTask obj)
    {
        return base.GetHashCode();
    }
}
