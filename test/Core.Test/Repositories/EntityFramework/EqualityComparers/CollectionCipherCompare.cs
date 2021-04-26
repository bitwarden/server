using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.Models.Table;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class CollectionCipherCompare: IEqualityComparer<CollectionCipher>
    {
        public bool Equals(CollectionCipher x, CollectionCipher y)
        {
            return true;
        }

        public int GetHashCode([DisallowNull] CollectionCipher obj)
        {
            return base.GetHashCode();
        }
    }
}
