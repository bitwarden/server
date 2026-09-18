using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Duende.IdentityServer.Models;
using Xunit;

namespace Bit.Identity.Test.IdentityServer;

public class StaticClientStoreTests
{
    private const string TestVaultUri = "https://vault.example.com";

    private static readonly string[] InteractiveClients =
    {
        BitwardenClient.Mobile,
        BitwardenClient.Web,
        BitwardenClient.Browser,
        BitwardenClient.Desktop,
        BitwardenClient.Cli,
        BitwardenClient.DirectoryConnector,
    };

    private static GlobalSettings NewSettings()
    {
        var s = new GlobalSettings();
        s.BaseServiceUri.Vault = TestVaultUri;
        return s;
    }

    [Theory]
    [InlineData(BitwardenClient.Mobile, 3600)]
    [InlineData(BitwardenClient.Web, 3600)]
    [InlineData(BitwardenClient.Browser, 3600)]
    [InlineData(BitwardenClient.Desktop, 3600)]
    [InlineData(BitwardenClient.Cli, 3600)]
    [InlineData(BitwardenClient.DirectoryConnector, 24 * 3600)]
    public void AccessTokenLifetime_MatchesHardcodedDefault(string clientId, int expectedSeconds)
    {
        var sut = new StaticClientStore(NewSettings());

        Assert.Equal(expectedSeconds, sut.Clients[clientId].AccessTokenLifetime);
    }

    [Theory]
    [InlineData(BitwardenClient.Mobile, 60 * 86400)]
    [InlineData(BitwardenClient.Web, 7 * 86400)]
    [InlineData(BitwardenClient.Browser, 30 * 86400)]
    [InlineData(BitwardenClient.Desktop, 30 * 86400)]
    [InlineData(BitwardenClient.Cli, 30 * 86400)]
    [InlineData(BitwardenClient.DirectoryConnector, 30 * 86400)]
    public void SlidingRefreshTokenLifetime_DerivedFromPerClientDays_WhenNoOverride(string clientId, int expectedSeconds)
    {
        var sut = new StaticClientStore(NewSettings());

        Assert.Equal(expectedSeconds, sut.Clients[clientId].SlidingRefreshTokenLifetime);
    }

    [Theory]
    [InlineData(BitwardenClient.Mobile)]
    [InlineData(BitwardenClient.Web)]
    [InlineData(BitwardenClient.Browser)]
    [InlineData(BitwardenClient.Desktop)]
    [InlineData(BitwardenClient.Cli)]
    [InlineData(BitwardenClient.DirectoryConnector)]
    public void SlidingRefreshTokenLifetime_UsesGlobalOverride_WhenSet(string clientId)
    {
        var settings = NewSettings();
        settings.IdentityServer.SlidingRefreshTokenLifetimeSeconds = 12345;

        var sut = new StaticClientStore(settings);

        Assert.Equal(12345, sut.Clients[clientId].SlidingRefreshTokenLifetime);
    }

    [Theory]
    [InlineData(BitwardenClient.Mobile)]
    [InlineData(BitwardenClient.Web)]
    [InlineData(BitwardenClient.Browser)]
    [InlineData(BitwardenClient.Desktop)]
    [InlineData(BitwardenClient.Cli)]
    [InlineData(BitwardenClient.DirectoryConnector)]
    public void AbsoluteRefreshTokenLifetime_DefaultsToZero(string clientId)
    {
        var sut = new StaticClientStore(NewSettings());

        Assert.Equal(0, sut.Clients[clientId].AbsoluteRefreshTokenLifetime);
    }

    [Theory]
    [InlineData(BitwardenClient.Mobile)]
    [InlineData(BitwardenClient.Web)]
    [InlineData(BitwardenClient.Browser)]
    [InlineData(BitwardenClient.Desktop)]
    [InlineData(BitwardenClient.Cli)]
    [InlineData(BitwardenClient.DirectoryConnector)]
    public void AbsoluteRefreshTokenLifetime_UsesGlobalOverride_WhenSet(string clientId)
    {
        var settings = NewSettings();
        settings.IdentityServer.AbsoluteRefreshTokenLifetimeSeconds = 99999;

        var sut = new StaticClientStore(settings);

        Assert.Equal(99999, sut.Clients[clientId].AbsoluteRefreshTokenLifetime);
    }

    [Theory]
    [InlineData(false, TokenExpiration.Sliding)]
    [InlineData(true, TokenExpiration.Absolute)]
    public void RefreshTokenExpiration_DerivedFromApplyAbsoluteFlag(bool absoluteFlag, TokenExpiration expected)
    {
        var settings = NewSettings();
        settings.IdentityServer.ApplyAbsoluteExpirationOnRefreshToken = absoluteFlag;

        var sut = new StaticClientStore(settings);

        foreach (var id in InteractiveClients)
        {
            Assert.Equal(expected, sut.Clients[id].RefreshTokenExpiration);
        }
    }

    [Theory]
    [InlineData(BitwardenClient.Mobile)]
    [InlineData(BitwardenClient.Web)]
    [InlineData(BitwardenClient.Browser)]
    [InlineData(BitwardenClient.Desktop)]
    [InlineData(BitwardenClient.Cli)]
    [InlineData(BitwardenClient.DirectoryConnector)]
    public void SharedClientDefaults_AreSetIdenticallyForAllInteractiveClients(string clientId)
    {
        var client = new StaticClientStore(NewSettings()).Clients[clientId];

        Assert.Equal(clientId, client.ClientId);
        Assert.Equal(TokenUsage.ReUse, client.RefreshTokenUsage);
        Assert.True(client.UpdateAccessTokenClaimsOnRefresh);
        Assert.True(client.AllowOfflineAccess);
        Assert.False(client.RequireConsent);
        Assert.True(client.RequirePkce);
        Assert.False(client.RequireClientSecret);
        Assert.Equal(new[] { "api" }, client.AllowedScopes);
        Assert.Contains("password", client.AllowedGrantTypes);
        Assert.Contains("authorization_code", client.AllowedGrantTypes);
        Assert.Contains("webauthn", client.AllowedGrantTypes);
    }

    [Theory]
    [InlineData(BitwardenClient.Web)]
    [InlineData(BitwardenClient.Browser)]
    public void WebAndBrowser_RedirectAndCorsUris_UseVaultUri(string clientId)
    {
        var client = new StaticClientStore(NewSettings()).Clients[clientId];

        Assert.Equal(new[] { $"{TestVaultUri}/sso-connector.html" }, client.RedirectUris);
        Assert.Equal(new[] { TestVaultUri }, client.PostLogoutRedirectUris);
        Assert.Equal(new[] { TestVaultUri }, client.AllowedCorsOrigins);
    }

    [Fact]
    public void Desktop_RedirectUris_IncludeSchemeCallbackAndLocalhostPorts()
    {
        var client = new StaticClientStore(NewSettings()).Clients[BitwardenClient.Desktop];

        Assert.Contains("bitwarden://sso-callback", client.RedirectUris);
        foreach (var port in Enumerable.Range(8065, 6))
        {
            Assert.Contains($"http://localhost:{port}", client.RedirectUris);
        }
        Assert.Equal(new[] { "bitwarden://logged-out" }, client.PostLogoutRedirectUris);
        Assert.Empty(client.AllowedCorsOrigins);
    }

    [Fact]
    public void DirectoryConnector_RedirectUris_IncludeLocalhostPortsAndBwdcCallback()
    {
        var client = new StaticClientStore(NewSettings()).Clients[BitwardenClient.DirectoryConnector];

        foreach (var port in Enumerable.Range(8065, 6))
        {
            Assert.Contains($"http://localhost:{port}", client.RedirectUris);
            Assert.Contains($"http://localhost:{port}", client.PostLogoutRedirectUris);
        }
        Assert.Contains("bwdc://sso-callback", client.RedirectUris);
        Assert.Contains("bwdc://logged-out", client.PostLogoutRedirectUris);
        Assert.Empty(client.AllowedCorsOrigins);
    }

    [Fact]
    public void Cli_RedirectAndPostLogoutUris_AreLocalhostPortsOnly()
    {
        var client = new StaticClientStore(NewSettings()).Clients[BitwardenClient.Cli];

        var expected = Enumerable.Range(8065, 6).Select(p => $"http://localhost:{p}").ToArray();
        Assert.Equal(expected, client.RedirectUris);
        Assert.Equal(expected, client.PostLogoutRedirectUris);
        Assert.Empty(client.AllowedCorsOrigins);
    }

    [Fact]
    public void Mobile_RedirectUris_UseMobileSsoConstants()
    {
        var client = new StaticClientStore(NewSettings()).Clients[BitwardenClient.Mobile];

        Assert.Equal(Constants.BitwardenMobileSsoCallbackUris, client.RedirectUris);
        Assert.Equal(new[] { "bitwarden://logged-out" }, client.PostLogoutRedirectUris);
        Assert.Empty(client.AllowedCorsOrigins);
    }

    [Fact]
    public void Clients_IncludesSendClient()
    {
        var sut = new StaticClientStore(NewSettings());

        Assert.True(sut.Clients.ContainsKey(BitwardenClient.Send));
    }
}
