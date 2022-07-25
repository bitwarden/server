using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
    public class SecretCompare : IEqualityComparer<Secret>
    {
        public bool Equals(Secret x, Secret y)
        {
            return x.Id == y.Id &&
                x.OrganizationId == y.OrganizationId &&
                x.Key == y.Key &&
                x.Value == y.Value &&
                x.Note == y.Note;
        }

        public int GetHashCode([DisallowNull] Secret obj)
        {
            return base.GetHashCode();
        }
    }
}

