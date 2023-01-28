using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class AuthRequestCompare : IEqualityComparer<AuthRequest>
{
    public bool Equals(AuthRequest x, AuthRequest y)
    {
        return x.AccessCode == y.AccessCode &&
        x.MasterPasswordHash == y.MasterPasswordHash &&
        x.PublicKey == y.PublicKey &&
        x.RequestDeviceIdentifier == y.RequestDeviceIdentifier &&
        x.RequestDeviceType == y.RequestDeviceType &&
        x.RequestIpAddress == y.RequestIpAddress &&
        x.RequestFingerprint == y.RequestFingerprint;
    }

    public int GetHashCode([DisallowNull] AuthRequest obj)
    {
        return base.GetHashCode();
    }
}
