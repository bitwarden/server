using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response.TwoFactor;

public class TwoFactorYubiKeyDetailsTests
{
    [Fact]
    public void Ctor_NullUser_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TwoFactorYubiKeyDetails(null!));
    }

    [Theory, BitAutoData]
    public void Ctor_WithAllKeysAndNfc_PopulatesAll(User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.YubiKey] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object>
                {
                    ["Key1"] = "ccccccccccbe",
                    ["Key2"] = "ccccccccccbf",
                    ["Key3"] = "ccccccccccbg",
                    ["Key4"] = "ccccccccccbh",
                    ["Key5"] = "ccccccccccbi",
                    ["Nfc"] = true,
                },
            },
        });

        var details = new TwoFactorYubiKeyDetails(user);

        Assert.True(details.Enabled);
        Assert.Equal("ccccccccccbe", details.Key1);
        Assert.Equal("ccccccccccbf", details.Key2);
        Assert.Equal("ccccccccccbg", details.Key3);
        Assert.Equal("ccccccccccbh", details.Key4);
        Assert.Equal("ccccccccccbi", details.Key5);
        Assert.True(details.Nfc);
    }

    [Theory, BitAutoData]
    public void Ctor_WithSingleKeyOnly_OtherKeysNull(User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.YubiKey] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object>
                {
                    ["Key1"] = "ccccccccccbe",
                    ["Nfc"] = false,
                },
            },
        });

        var details = new TwoFactorYubiKeyDetails(user);

        Assert.True(details.Enabled);
        Assert.Equal("ccccccccccbe", details.Key1);
        Assert.Null(details.Key2);
        Assert.Null(details.Key3);
        Assert.Null(details.Key4);
        Assert.Null(details.Key5);
        Assert.False(details.Nfc);
    }

    [Theory, BitAutoData]
    public void Ctor_WithEmptyMetaData_DisabledFalse(User user)
    {
        user.TwoFactorProviders = "{\"3\":{}}";

        var details = new TwoFactorYubiKeyDetails(user);

        Assert.False(details.Enabled);
        Assert.Null(details.Key1);
        Assert.False(details.Nfc);
    }

    [Theory, BitAutoData]
    public void Ctor_NullTwoFactorProviders_DisabledFalse(User user)
    {
        user.TwoFactorProviders = null;

        var details = new TwoFactorYubiKeyDetails(user);

        Assert.False(details.Enabled);
        Assert.Null(details.Key1);
    }

    [Theory, BitAutoData]
    public void Ctor_ProviderDisabled_KeysPopulated_EnabledFalse(User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.YubiKey] = new TwoFactorProvider
            {
                Enabled = false,
                MetaData = new Dictionary<string, object>
                {
                    ["Key1"] = "ccccccccccbe",
                    ["Nfc"] = true,
                },
            },
        });

        var details = new TwoFactorYubiKeyDetails(user);

        Assert.False(details.Enabled);
        Assert.Equal("ccccccccccbe", details.Key1);
        Assert.True(details.Nfc);
    }
}
