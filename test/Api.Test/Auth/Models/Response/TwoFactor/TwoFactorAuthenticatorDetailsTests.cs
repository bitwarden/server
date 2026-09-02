using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using OtpNet;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response.TwoFactor;

public class TwoFactorAuthenticatorDetailsTests
{
    [Fact]
    public void Ctor_NullUser_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TwoFactorAuthenticatorDetails(null!));
    }

    [Theory, BitAutoData]
    public void Ctor_NoExistingProvider_MintsRandomBase32Key_DisabledFalse(User user)
    {
        user.TwoFactorProviders = null;

        var details = new TwoFactorAuthenticatorDetails(user);

        Assert.False(details.Enabled);
        Assert.False(string.IsNullOrEmpty(details.Key));
        // The minted key must be valid Base32 (decodable via OtpNet) so the client can render a QR.
        var decoded = Base32Encoding.ToBytes(details.Key);
        Assert.Equal(20, decoded.Length);
    }

    [Theory, BitAutoData]
    public void Ctor_ExistingProviderEnabled_UsesStoredKey_EnabledTrue(User user)
    {
        const string storedKey = "JBSWY3DPEHPK3PXP";
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Authenticator] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object> { ["Key"] = storedKey },
            },
        });

        var details = new TwoFactorAuthenticatorDetails(user);

        Assert.True(details.Enabled);
        Assert.Equal(storedKey, details.Key);
    }

    [Theory, BitAutoData]
    public void Ctor_ExistingProviderDisabled_UsesStoredKey_EnabledFalse(User user)
    {
        const string storedKey = "JBSWY3DPEHPK3PXP";
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Authenticator] = new TwoFactorProvider
            {
                Enabled = false,
                MetaData = new Dictionary<string, object> { ["Key"] = storedKey },
            },
        });

        var details = new TwoFactorAuthenticatorDetails(user);

        Assert.False(details.Enabled);
        Assert.Equal(storedKey, details.Key);
    }

    [Theory, BitAutoData]
    public void Ctor_NoExistingProvider_ConsecutiveCallsYieldDistinctKeys(User userA, User userB)
    {
        userA.TwoFactorProviders = null;
        userB.TwoFactorProviders = null;

        var a = new TwoFactorAuthenticatorDetails(userA);
        var b = new TwoFactorAuthenticatorDetails(userB);

        Assert.NotEqual(a.Key, b.Key);
    }
}
