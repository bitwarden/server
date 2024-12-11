using System.Diagnostics.CodeAnalysis;
using Bit.Core.Vault.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class CipherCompare : IEqualityComparer<Cipher>
{
    public bool Equals(Cipher x, Cipher y)
    {
        return x.Type == y.Type
            && x.Data == y.Data
            && x.Favorites == y.Favorites
            && x.Attachments == y.Attachments;
    }

    public int GetHashCode([DisallowNull] Cipher obj)
    {
        return base.GetHashCode();
    }
}
