using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

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
