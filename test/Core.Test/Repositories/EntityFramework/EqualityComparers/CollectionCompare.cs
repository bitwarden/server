using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class CollectionCompare : IEqualityComparer<Collection>
    {
        public bool Equals(Collection x, Collection y)
        {
            return x.Name == y.Name &&
                x.ExternalId == y.ExternalId;
        }

        public int GetHashCode([DisallowNull] Collection obj)
        {
            return base.GetHashCode();
        }
    }
}
