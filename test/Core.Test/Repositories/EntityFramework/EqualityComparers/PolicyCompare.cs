using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.Models.Table;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class PolicyCompare: IEqualityComparer<Policy>
    {
        public bool Equals(Policy x, Policy y)
        {
            return  x.Type == y.Type &&
            x.Data == y.Data &&
            x.Enabled == y.Enabled;
        }

        public int GetHashCode([DisallowNull] Policy obj)
        {
            return base.GetHashCode();
        }
    }
}
