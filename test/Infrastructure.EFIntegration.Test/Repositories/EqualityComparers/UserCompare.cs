using System.Diagnostics.CodeAnalysis;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;

public class UserCompare : IEqualityComparer<User>
{
    public bool Equals(User x, User y)
    {
        return x.Name == y.Name
            && x.Email == y.Email
            && x.EmailVerified == y.EmailVerified
            && x.MasterPassword == y.MasterPassword
            && x.MasterPasswordHint == y.MasterPasswordHint
            && x.Culture == y.Culture
            && x.SecurityStamp == y.SecurityStamp
            && x.TwoFactorProviders == y.TwoFactorProviders
            && x.TwoFactorRecoveryCode == y.TwoFactorRecoveryCode
            && x.EquivalentDomains == y.EquivalentDomains
            && x.Key == y.Key
            && x.PublicKey == y.PublicKey
            && x.PrivateKey == y.PrivateKey
            && x.Premium == y.Premium
            && x.Storage == y.Storage
            && x.MaxStorageGb == y.MaxStorageGb
            && x.Gateway == y.Gateway
            && x.GatewayCustomerId == y.GatewayCustomerId
            && x.ReferenceData == y.ReferenceData
            && x.LicenseKey == y.LicenseKey
            && x.ApiKey == y.ApiKey
            && x.Kdf == y.Kdf
            && x.KdfIterations == y.KdfIterations;
    }

    public int GetHashCode([DisallowNull] User obj)
    {
        return base.GetHashCode();
    }
}
