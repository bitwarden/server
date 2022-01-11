using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
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
}
