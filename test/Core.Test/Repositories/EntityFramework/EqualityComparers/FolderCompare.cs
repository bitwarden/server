using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.Models.Table;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class FolderCompare : IEqualityComparer<Folder>
    {
        public bool Equals(Folder x, Folder y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode([DisallowNull] Folder obj)
        {
            return base.GetHashCode();
        }
    }
}
