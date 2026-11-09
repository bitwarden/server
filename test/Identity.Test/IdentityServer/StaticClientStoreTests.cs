using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Bit.Identity.Utilities;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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

    private static StaticClientStore Build(GlobalSettings globalSettings, ILogger<StaticClientStore>? logger = null)
    {
        return new StaticClientStore(globalSettings, logger ?? NullLogger<StaticClientStore>.Instance);
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
        var sut = Build(NewSettings());

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
        var sut = Build(NewSettings());

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

        var sut = Build(settings);

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
        var sut = Build(NewSettings());

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

        var sut = Build(settings);

        Assert.Equal(99999, sut.Clients[clientId].AbsoluteRefreshTokenLifetime);
    }

    [Theory]
    [InlineData(false, TokenExpiration.Sliding)]
    [InlineData(true, TokenExpiration.Absolute)]
    public void RefreshTokenExpiration_DerivedFromApplyAbsoluteFlag(bool absoluteFlag, TokenExpiration expected)
    {
        var settings = NewSettings();
        settings.IdentityServer.ApplyAbsoluteExpirationOnRefreshToken = absoluteFlag;

        var sut = Build(settings);

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
        var client = Build(NewSettings()).Clients[clientId];

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
        var client = Build(NewSettings()).Clients[clientId];

        Assert.Equal(new[] { $"{TestVaultUri}/sso-connector.html" }, client.RedirectUris);
        Assert.Equal(new[] { TestVaultUri }, client.PostLogoutRedirectUris);
        Assert.Equal(new[] { TestVaultUri }, client.AllowedCorsOrigins);
    }

    [Fact]
    public void Desktop_RedirectUris_IncludeSchemeCallbackAndLocalhostPorts()
    {
        var client = Build(NewSettings()).Clients[BitwardenClient.Desktop];

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
        var client = Build(NewSettings()).Clients[BitwardenClient.DirectoryConnector];

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
        var client = Build(NewSettings()).Clients[BitwardenClient.Cli];

        var expected = Enumerable.Range(8065, 6).Select(p => $"http://localhost:{p}").ToArray();
        Assert.Equal(expected, client.RedirectUris);
        Assert.Equal(expected, client.PostLogoutRedirectUris);
        Assert.Empty(client.AllowedCorsOrigins);
    }

    [Fact]
    public void Mobile_RedirectUris_UseMobileSsoConstants()
    {
        var client = Build(NewSettings()).Clients[BitwardenClient.Mobile];

        Assert.Equal(Constants.BitwardenMobileSsoCallbackUris, client.RedirectUris);
        Assert.Equal(new[] { "bitwarden://logged-out" }, client.PostLogoutRedirectUris);
        Assert.Empty(client.AllowedCorsOrigins);
    }

    [Fact]
    public void Clients_IncludesSendClient()
    {
        var sut = Build(NewSettings());

        Assert.True(sut.Clients.ContainsKey(BitwardenClient.Send));
    }

    [Theory]
    [InlineData(BitwardenClient.Mobile)]
    [InlineData(BitwardenClient.Web)]
    [InlineData(BitwardenClient.Browser)]
    [InlineData(BitwardenClient.Desktop)]
    [InlineData(BitwardenClient.Cli)]
    public void Override_AppliesToInteractiveClients(string clientId)
    {
        var settings = NewSettings();
        settings.IdentityServer.AccessTokenLifetimeSeconds = 900;

        var sut = Build(settings);

        Assert.Equal(900, sut.Clients[clientId].AccessTokenLifetime);
    }

    [Fact]
    public void Override_DoesNotAffectDirectoryConnector()
    {
        var settings = NewSettings();
        settings.IdentityServer.AccessTokenLifetimeSeconds = 900;

        var sut = Build(settings);

        Assert.Equal(24 * 3600, sut.Clients[BitwardenClient.DirectoryConnector].AccessTokenLifetime);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(599)]
    public void Override_BelowRecommendedMinimum_LogsWarning(int seconds)
    {
        var logger = Substitute.For<ILogger<StaticClientStore>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var settings = NewSettings();
        settings.IdentityServer.AccessTokenLifetimeSeconds = seconds;

        Build(settings, logger);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("refresh threshold")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Theory]
    [InlineData(600)]
    [InlineData(900)]
    [InlineData(3600)]
    public void Override_AtOrAboveRecommendedMinimum_DoesNotWarn(int seconds)
    {
        var logger = Substitute.For<ILogger<StaticClientStore>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var settings = NewSettings();
        settings.IdentityServer.AccessTokenLifetimeSeconds = seconds;

        Build(settings, logger);

        logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StartupValidation_RejectsNonPositiveOverride(int badValue)
    {
        var settings = NewSettings();
        settings.IdentityServer.AccessTokenLifetimeSeconds = badValue;

        var ex = Assert.Throws<InvalidOperationException>(
            () => ServiceCollectionExtensions.ValidateAccessTokenLifetimeOverride(settings));

        Assert.Contains("accessTokenLifetimeSeconds", ex.Message);
    }

    [Fact]
    public void StartupValidation_AllowsPositiveOverride()
    {
        var settings = NewSettings();
        settings.IdentityServer.AccessTokenLifetimeSeconds = 60;

        ServiceCollectionExtensions.ValidateAccessTokenLifetimeOverride(settings);
    }

    [Fact]
    public void StartupValidation_AllowsNull()
    {
        ServiceCollectionExtensions.ValidateAccessTokenLifetimeOverride(NewSettings());
    }
}
