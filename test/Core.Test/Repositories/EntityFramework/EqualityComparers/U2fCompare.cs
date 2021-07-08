using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.Models.Table;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class U2fCompare: IEqualityComparer<U2f>
    {
        public bool Equals(U2f x, U2f y)
        {
            return  x.KeyHandle == y.KeyHandle &&
            x.Challenge == y.Challenge &&
            x.AppId == y.AppId &&
            x.Version == y.Version;
        }

        public int GetHashCode([DisallowNull] U2f obj)
        {
            return base.GetHashCode();
        }
    }
}
