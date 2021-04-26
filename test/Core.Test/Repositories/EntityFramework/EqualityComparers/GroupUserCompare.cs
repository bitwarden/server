using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.Models.Table;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class GroupUserCompare: IEqualityComparer<GroupUser>
    {
        public bool Equals(GroupUser x, GroupUser y)
        {
            return true;
        }

        public int GetHashCode([DisallowNull] GroupUser obj)
        {
            return base.GetHashCode();
        }
    }
}
