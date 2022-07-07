using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Core.Test.Repositories.EntityFramework.EqualityComparers
{
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
}
