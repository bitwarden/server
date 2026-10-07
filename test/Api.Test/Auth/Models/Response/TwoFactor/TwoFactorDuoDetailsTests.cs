using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response.TwoFactor;

public class TwoFactorDuoDetailsTests
{
    // ---------------------------------------------------------------------
    // User-scoped
    // ---------------------------------------------------------------------

    [Fact]
    public void Ctor_User_NullUser_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TwoFactorDuoDetails(null as User));
    }

    [Theory, BitAutoData]
    public void Ctor_User_WithProvider_BuildsModelAndMasksClientSecret(User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Duo] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object>
                {
                    ["Host"] = "example.com",
                    ["ClientId"] = "clientId",
                    ["ClientSecret"] = "secretClientSecret",
                },
            },
        });

        var details = new TwoFactorDuoDetails(user);

        Assert.True(details.Enabled);
        Assert.Equal("example.com", details.Host);
        Assert.Equal("clientId", details.ClientId);
        Assert.Equal("secret************", details.ClientSecret);
    }

    [Theory, BitAutoData]
    public void Ctor_User_WithEmptyMetaData_DisabledFalse(User user)
    {
        user.TwoFactorProviders = "{\"2\":{}}";

        var details = new TwoFactorDuoDetails(user);

        Assert.False(details.Enabled);
        Assert.Null(details.Host);
        Assert.Null(details.ClientId);
        Assert.Null(details.ClientSecret);
    }

    [Theory, BitAutoData]
    public void Ctor_User_NullTwoFactorProviders_DisabledFalse(User user)
    {
        user.TwoFactorProviders = null;

        var details = new TwoFactorDuoDetails(user);

        Assert.False(details.Enabled);
        Assert.Null(details.Host);
        Assert.Null(details.ClientId);
        Assert.Null(details.ClientSecret);
    }

    [Theory, BitAutoData]
    public void Ctor_User_PartialMetaData_OnlyPresentFieldsPopulated(User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Duo] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object> { ["Host"] = "example.com" },
            },
        });

        var details = new TwoFactorDuoDetails(user);

        Assert.True(details.Enabled);
        Assert.Equal("example.com", details.Host);
        Assert.Null(details.ClientId);
        Assert.Null(details.ClientSecret);
    }

    // ---------------------------------------------------------------------
    // Organization-scoped
    // ---------------------------------------------------------------------

    [Fact]
    public void Ctor_Organization_NullOrganization_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TwoFactorDuoDetails(null as Organization));
    }

    [Theory, BitAutoData]
    public void Ctor_Organization_WithProvider_BuildsModelAndMasksClientSecret(Organization organization)
    {
        organization.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.OrganizationDuo] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object>
                {
                    ["Host"] = "example.com",
                    ["ClientId"] = "clientId",
                    ["ClientSecret"] = "secretClientSecret",
                },
            },
        });

        var details = new TwoFactorDuoDetails(organization);

        Assert.True(details.Enabled);
        Assert.Equal("example.com", details.Host);
        Assert.Equal("clientId", details.ClientId);
        Assert.Equal("secret************", details.ClientSecret);
    }

    [Theory, BitAutoData]
    public void Ctor_Organization_WithEmptyMetaData_DisabledFalse(Organization organization)
    {
        organization.TwoFactorProviders = "{\"6\":{}}";

        var details = new TwoFactorDuoDetails(organization);

        Assert.False(details.Enabled);
    }

    [Theory, BitAutoData]
    public void Ctor_Organization_NullTwoFactorProviders_DisabledFalse(Organization organization)
    {
        organization.TwoFactorProviders = null;

        var details = new TwoFactorDuoDetails(organization);

        Assert.False(details.Enabled);
    }

    // ---------------------------------------------------------------------
    // Masking edge cases (private MaskSecret exercised via ClientSecret)
    // ---------------------------------------------------------------------

    [Theory, BitAutoData]
    public void ClientSecret_SixCharsOrFewer_ReturnedUnmasked(User user)
    {
        // The mask logic preserves the first 6 chars; secrets at or below that length
        // would mask to nothing and are returned as-is.
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Duo] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object> { ["ClientSecret"] = "abc" },
            },
        });

        var details = new TwoFactorDuoDetails(user);

        Assert.Equal("abc", details.ClientSecret);
    }
}
