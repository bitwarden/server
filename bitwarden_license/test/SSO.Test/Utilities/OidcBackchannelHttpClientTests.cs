using Bit.Core.Business.Sso;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Sso.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SSO.Test.Utilities;

/// <summary>
/// Covers the SSRF guard on the OpenID Connect backchannel. Authority and MetadataAddress come
/// from the organization's own SSO configuration and the discovery fetch is reachable without
/// authentication via /sso/prevalidate, so the backchannel must not be able to reach internal
/// addresses on cloud.
/// </summary>
public class OidcBackchannelHttpClientTests
{
    private static HttpClient CreateOidcBackchannel(bool selfHosted)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSsoServices(new GlobalSettings { SelfHosted = selfHosted });

        return services.BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(DynamicAuthenticationSchemeProvider.OidcBackchannelHttpClientName);
    }

    [Theory]
    [InlineData("https://127.0.0.1/.well-known/openid-configuration")]    // loopback
    [InlineData("https://10.0.0.1/.well-known/openid-configuration")]     // RFC 1918
    [InlineData("https://192.168.1.1/.well-known/openid-configuration")]  // RFC 1918
    [InlineData("https://169.254.169.254/latest/meta-data/")]             // cloud metadata
    [InlineData("https://168.63.129.16/metadata/instance")]               // Azure wireserver
    [InlineData("https://[::1]/.well-known/openid-configuration")]        // IPv6 loopback
    public async Task Cloud_InternalAuthority_IsBlockedBeforeConnecting(string url)
    {
        var client = CreateOidcBackchannel(selfHosted: false);

        await Assert.ThrowsAsync<SsrfProtectionException>(() => client.GetAsync(url));
    }

    [Fact]
    public async Task Cloud_HostnameResolvingToInternalIp_IsBlocked()
    {
        // The guard resolves DNS itself on every request, so a hostname pointed at an internal
        // address is blocked just like a literal one. This is what defeats DNS rebinding.
        var client = CreateOidcBackchannel(selfHosted: false);

        await Assert.ThrowsAsync<SsrfProtectionException>(
            () => client.GetAsync("https://localhost/.well-known/openid-configuration"));
    }

    [Fact]
    public async Task SelfHosted_InternalAuthority_IsNotBlocked()
    {
        // Self-hosted installations routinely run their IdP on an internal address and the
        // operator already owns that network, so the guard is cloud-only. The request is
        // attempted and fails on connection rather than being rejected up front.
        var client = CreateOidcBackchannel(selfHosted: true);

        var exception = await Record.ExceptionAsync(
            () => client.GetAsync("https://127.0.0.1:1/.well-known/openid-configuration"));

        Assert.IsNotType<SsrfProtectionException>(exception);
        Assert.IsType<HttpRequestException>(exception);
    }

    [Fact]
    public void MatchesTheBackchannelDefaultsTheFrameworkWouldHaveApplied()
    {
        // Supplying our own Backchannel skips the client OpenIdConnectPostConfigureOptions would
        // otherwise build, so its defaults have to be reproduced on the named client.
        var client = CreateOidcBackchannel(selfHosted: false);

        Assert.Equal(TimeSpan.FromMinutes(1), client.Timeout);
        Assert.Equal(1024 * 1024 * 10, client.MaxResponseContentBufferSize);
        Assert.Equal(
            "Microsoft ASP.NET Core OpenIdConnect handler",
            client.DefaultRequestHeaders.UserAgent.ToString());
    }
}
