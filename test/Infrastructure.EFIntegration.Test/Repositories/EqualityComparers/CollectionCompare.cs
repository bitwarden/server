using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class CollectionCompare : IEqualityComparer<Collection>
{
    public bool Equals(Collection x, Collection y)
    {
        return x.Name == y.Name && x.ExternalId == y.ExternalId;
    }

    public int GetHashCode([DisallowNull] Collection obj)
    {
        return base.GetHashCode();
    }
}
