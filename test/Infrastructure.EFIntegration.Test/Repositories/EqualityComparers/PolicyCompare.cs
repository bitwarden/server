using System.Diagnostics.CodeAnalysis;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class PolicyCompare : IEqualityComparer<Policy>
{
    public bool Equals(Policy x, Policy y)
    {
        return x.Type == y.Type && x.Data == y.Data && x.Enabled == y.Enabled;
    }

    public int GetHashCode([DisallowNull] Policy obj)
    {
        return base.GetHashCode();
    }
}

public class PolicyCompareIncludingOrganization : PolicyCompare
{
    public new bool Equals(Policy x, Policy y)
    {
        return base.Equals(x, y) && x.OrganizationId == y.OrganizationId;
    }
}
