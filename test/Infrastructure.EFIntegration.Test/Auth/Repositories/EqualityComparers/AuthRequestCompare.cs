using System.Diagnostics.CodeAnalysis;
using Bit.Core.Auth.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Auth.Repositories.EqualityComparers;

public class AuthRequestCompare : IEqualityComparer<AuthRequest>
{
    public bool Equals(AuthRequest x, AuthRequest y)
    {
        return x.AccessCode == y.AccessCode
            && x.MasterPasswordHash == y.MasterPasswordHash
            && x.PublicKey == y.PublicKey
            && x.RequestDeviceIdentifier == y.RequestDeviceIdentifier
            && x.RequestDeviceType == y.RequestDeviceType
            && x.RequestIpAddress == y.RequestIpAddress;
    }

    public int GetHashCode([DisallowNull] AuthRequest obj)
    {
        return base.GetHashCode();
    }
}
