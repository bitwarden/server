using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class TaxRateCompare : IEqualityComparer<TaxRate>
{
    public bool Equals(TaxRate x, TaxRate y)
    {
        return x.Country == y.Country
            && x.State == y.State
            && x.PostalCode == y.PostalCode
            && x.Rate == y.Rate
            && x.Active == y.Active;
    }

    public int GetHashCode([DisallowNull] TaxRate obj)
    {
        return base.GetHashCode();
    }
}
