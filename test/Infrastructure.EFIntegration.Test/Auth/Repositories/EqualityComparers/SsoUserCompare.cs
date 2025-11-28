using System.Diagnostics.CodeAnalysis;
using Bit.Core.Auth.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Auth.Repositories.EqualityComparers;

public class SsoUserCompare : IEqualityComparer<SsoUser>
{
    public bool Equals(SsoUser x, SsoUser y)
    {
        return x.ExternalId == y.ExternalId;
    }

    public int GetHashCode([DisallowNull] SsoUser obj)
    {
        return base.GetHashCode();
    }
}
