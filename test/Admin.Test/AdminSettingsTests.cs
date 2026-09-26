using Bit.Admin;

namespace Admin.Test;

public class AdminSettingsTests
{
    [Fact]
    public void OidcEnabled_IsFalse_WhenOidcSectionMissing()
    {
        var settings = new AdminSettings { Oidc = null };

        Assert.False(settings.OidcEnabled);
    }

    [Fact]
    public void OidcEnabled_IsFalse_WhenAuthorityMissing()
    {
        var settings = BuildOidcSettings();
        settings.Oidc.Authority = null;

        Assert.False(settings.OidcEnabled);
    }

    [Fact]
    public void OidcEnabled_IsFalse_WhenClientIdMissing()
    {
        var settings = BuildOidcSettings();
        settings.Oidc.ClientId = "";

        Assert.False(settings.OidcEnabled);
    }

    [Fact]
    public void OidcEnabled_IsFalse_WhenClientSecretMissing()
    {
        var settings = BuildOidcSettings();
        settings.Oidc.ClientSecret = "   ";

        Assert.False(settings.OidcEnabled);
    }

    [Fact]
    public void OidcEnabled_IsTrue_WhenAllRequiredFieldsPresent()
    {
        var settings = BuildOidcSettings();

        Assert.True(settings.OidcEnabled);
    }

    private static AdminSettings BuildOidcSettings() => new()
    {
        Oidc = new AdminSettings.OidcSettings
        {
            Authority = "https://idp.example.com",
            ClientId = "admin-portal",
            ClientSecret = "supersecret"
        }
    };
}
