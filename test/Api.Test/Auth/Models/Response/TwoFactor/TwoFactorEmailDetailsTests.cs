using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response.TwoFactor;

public class TwoFactorEmailDetailsTests
{
    [Fact]
    public void Ctor_NullUser_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TwoFactorEmailDetails(null!));
    }

    [Theory, BitAutoData]
    public void Ctor_WithProviderEnabled_PopulatesEmail_EnabledTrue(User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object> { ["Email"] = "user@example.com" },
            },
        });

        var details = new TwoFactorEmailDetails(user);

        Assert.True(details.Enabled);
        Assert.Equal("user@example.com", details.Email);
    }

    [Theory, BitAutoData]
    public void Ctor_WithProviderDisabled_PopulatesEmail_EnabledFalse(User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                Enabled = false,
                MetaData = new Dictionary<string, object> { ["Email"] = "user@example.com" },
            },
        });

        var details = new TwoFactorEmailDetails(user);

        Assert.False(details.Enabled);
        Assert.Equal("user@example.com", details.Email);
    }

    [Theory, BitAutoData]
    public void Ctor_WithProviderNoEmailMetaData_DisabledFalse_EmailNull(User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object>(),
            },
        });

        var details = new TwoFactorEmailDetails(user);

        Assert.False(details.Enabled);
        Assert.Null(details.Email);
    }

    [Theory, BitAutoData]
    public void Ctor_NullTwoFactorProviders_DisabledFalse_EmailNull(User user)
    {
        user.TwoFactorProviders = null;

        var details = new TwoFactorEmailDetails(user);

        Assert.False(details.Enabled);
        Assert.Null(details.Email);
    }
}
