using System.Net;
using System.Net.Sockets;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Sso.IntegrationTest.Utilities;
using Xunit;

namespace Bit.Sso.IntegrationTest.Controllers;

/// <summary>
/// An organization's OpenID Connect Authority is a URL the organization chooses, and PreValidate
/// makes the SSO server fetch discovery metadata from it. On cloud, those outbound requests must
/// never reach internal or private network addresses, because those belong to Bitwarden's
/// infrastructure rather than to the organization.
/// </summary>
/// <remarks>
/// Each test points the Authority at a loopback listener owned by the test, which stands in for an
/// internal host, and checks whether the SSO server opened a connection to it.
/// </remarks>
public class AccountControllerPreValidateTests
{
    /// <summary>
    /// The request to the internal address must be refused before any connection opens. PreValidate
    /// still returns an error, because the scheme cannot load its metadata.
    /// </summary>
    [Fact]
    public async Task PreValidate_Cloud_OidcAuthorityResolvesToLoopback_DoesNotConnectToAuthority()
    {
        // Arrange
        using var authority = LoopbackListener.Start();
        var identifier = $"prevalidate-{Guid.NewGuid()}";
        var testData = await new SsoTestDataBuilder()
            .WithOrganization(org => org.Identifier = identifier)
            .WithSsoConfig(ssoConfig => ssoConfig.SetData(OidcConfigurationFor(authority)))
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/Account/PreValidate?domainHint={identifier}");

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.False(authority.ConnectionReceived,
            "The SSO server opened a connection to an OIDC Authority on a loopback address.");
    }

    /// <summary>
    /// Self-hosted installations commonly run their IdP on an internal address, so the backchannel
    /// must still reach it. This also confirms the loopback listener is reachable from the test
    /// host, which keeps the cloud test above from passing for an unrelated reason.
    /// </summary>
    [Fact]
    public async Task PreValidate_SelfHosted_OidcAuthorityResolvesToLoopback_ConnectsToAuthority()
    {
        // Arrange
        using var authority = LoopbackListener.Start();
        var identifier = $"prevalidate-{Guid.NewGuid()}";
        var testData = await new SsoTestDataBuilder()
            .AsSelfHosted()
            .WithConfiguration("globalSettings:selfHosted", "true")
            // Self-hosted startup rejects an empty installation id.
            .WithConfiguration("globalSettings:installation:id", "10000000-0000-0000-0000-000000000000")
            .WithOrganization(org => org.Identifier = identifier)
            .WithSsoConfig(ssoConfig => ssoConfig.SetData(OidcConfigurationFor(authority)))
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        await client.GetAsync($"/Account/PreValidate?domainHint={identifier}");

        // Assert
        Assert.True(authority.ConnectionReceived,
            "The SSO server did not connect to an OIDC Authority on a loopback address.");
    }

    private static SsoConfigurationData OidcConfigurationFor(LoopbackListener authority) => new()
    {
        ConfigType = SsoType.OpenIdConnect,
        Authority = authority.HttpsUrl,
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
    };

    /// <summary>
    /// A TCP listener on 127.0.0.1 that records whether any client connected, then closes every
    /// connection immediately. Closing before the TLS handshake completes makes the caller's HTTPS
    /// request fail fast rather than wait for its timeout.
    /// </summary>
    private sealed class LoopbackListener : IDisposable
    {
        private readonly TcpListener _listener;
        private volatile bool _connectionReceived;

        private LoopbackListener(TcpListener listener)
        {
            _listener = listener;
        }

        public string HttpsUrl => $"https://{IPAddress.Loopback}:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        /// <summary>
        /// Set before the accepted connection is closed, so it is already <c>true</c> by the time the
        /// connecting caller observes the failure.
        /// </summary>
        public bool ConnectionReceived => _connectionReceived;

        public static LoopbackListener Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var loopbackListener = new LoopbackListener(listener);
            _ = loopbackListener.AcceptConnectionsAsync();
            return loopbackListener;
        }

        private async Task AcceptConnectionsAsync()
        {
            try
            {
                while (true)
                {
                    using var connection = await _listener.AcceptTcpClientAsync();
                    _connectionReceived = true;
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException)
            {
                // The listener was stopped.
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
