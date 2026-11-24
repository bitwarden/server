using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Core.Test.Auth.IdentityServer;

public class OpenIdConnectPartnerStoreTests
{
    private static OpenIdConnectPartnerStore BuildStore(params string[] clientIds)
    {
        var globalSettings = new GlobalSettings();
        globalSettings.IdentityServer.OpenIdConnectPartners = clientIds
            .Select(id => new GlobalSettings.IdentityServerSettings.OpenIdConnectPartnerSettings
            {
                ClientId = id,
                Name = $"{id} name",
                ClientSecretHash = "hash",
                RedirectUris = ["https://vault.example.com/oauth/complete"],
            })
            .ToArray();
        return new OpenIdConnectPartnerStore(globalSettings);
    }

    [Fact]
    public void GetByClientId_NormalizesPrefix()
    {
        var store = BuildStore("acme");

        // Configured without the prefix, resolvable with or without it.
        Assert.NotNull(store.GetByClientId("acme"));
        Assert.NotNull(store.GetByClientId("partner.acme"));
        Assert.Equal("partner.acme", store.GetByClientId("acme")!.ClientId);
    }

    [Fact]
    public void GetByClientId_AlreadyPrefixed_Resolves()
    {
        var store = BuildStore("partner.acme");

        Assert.NotNull(store.GetByClientId("partner.acme"));
        Assert.Equal("partner.acme", store.GetByClientId("partner.acme")!.ClientId);
    }

    [Fact]
    public void IsRegistered_UnknownClient_ReturnsFalse()
    {
        var store = BuildStore("acme");

        Assert.False(store.IsRegistered("partner.unknown"));
        Assert.True(store.IsRegistered("acme"));
    }

    [Fact]
    public void GetByClientId_NullOrEmpty_ReturnsNull()
    {
        var store = BuildStore("acme");

        Assert.Null(store.GetByClientId(null));
        Assert.Null(store.GetByClientId(""));
    }

    [Fact]
    public void GetAll_ReturnsAllConfiguredPartners()
    {
        var store = BuildStore("acme", "globex");

        Assert.Equal(2, store.GetAll().Count);
    }
}
