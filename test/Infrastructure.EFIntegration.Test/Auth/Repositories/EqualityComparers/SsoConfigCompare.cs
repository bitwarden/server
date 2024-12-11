using System.Diagnostics.CodeAnalysis;
using Bit.Core.Auth.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Auth.Repositories.EqualityComparers;

public class SsoConfigCompare : IEqualityComparer<SsoConfig>
{
    public bool Equals(SsoConfig x, SsoConfig y)
    {
        return x.Enabled == y.Enabled && x.OrganizationId == y.OrganizationId && x.Data == y.Data;
    }

    public int GetHashCode([DisallowNull] SsoConfig obj)
    {
        return base.GetHashCode();
    }
}
