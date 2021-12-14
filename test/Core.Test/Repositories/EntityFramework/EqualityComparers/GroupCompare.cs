using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.Models.Table;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class GroupCompare : IEqualityComparer<Group>
    {
        public bool Equals(Group x, Group y)
        {
            return x.Name == y.Name &&
            x.AccessAll == y.AccessAll &&
            x.ExternalId == y.ExternalId;
        }

        public int GetHashCode([DisallowNull] Group obj)
        {
            return base.GetHashCode();
        }
    }
}
