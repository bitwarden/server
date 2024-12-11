using System.Diagnostics.CodeAnalysis;
using Bit.Core.Auth.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Auth.Repositories.EqualityComparers;

public class GrantCompare : IEqualityComparer<Grant>
{
    public bool Equals(Grant x, Grant y)
    {
        return x.Key == y.Key
            && x.Type == y.Type
            && x.SubjectId == y.SubjectId
            && x.ClientId == y.ClientId
            && x.Description == y.Description
            && x.ExpirationDate == y.ExpirationDate
            && x.ConsumedDate == y.ConsumedDate
            && x.Data == y.Data;
    }

    public int GetHashCode([DisallowNull] Grant obj)
    {
        return base.GetHashCode();
    }
}
