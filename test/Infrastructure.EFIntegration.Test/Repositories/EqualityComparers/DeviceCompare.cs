using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class DeviceCompare : IEqualityComparer<Device>
{
    public bool Equals(Device x, Device y)
    {
        return x.Name == y.Name &&
            x.Type == y.Type &&
            x.Identifier == y.Identifier &&
            x.PushToken == y.PushToken;
    }

    public int GetHashCode([DisallowNull] Device obj)
    {
        return base.GetHashCode();
    }
}
