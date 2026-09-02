using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.ClientProviders;
using Duende.IdentityModel;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class InternalClientProviderTests
{
    private readonly GlobalSettings _globalSettings;

    private readonly InternalClientProvider _sut;

    public InternalClientProviderTests()
    {
        _globalSettings = new GlobalSettings
        {
            SelfHosted = true,
        };
        _sut = new InternalClientProvider(_globalSettings);
    }

    [Fact]
    public async Task GetAsync_ReturnsInternalClient()
    {
        var internalClient = await _sut.GetAsync("blah");

        Assert.NotNull(internalClient);
        Assert.Equal($"internal.blah", internalClient.ClientId);
        Assert.True(internalClient.RequireClientSecret);
        var secret = Assert.Single(internalClient.ClientSecrets);
        Assert.NotNull(secret);
        Assert.NotNull(secret.Value);
        var scope = Assert.Single(internalClient.AllowedScopes);
        Assert.Equal(ApiScopes.Internal, scope);
        Assert.Equal(TimeSpan.FromDays(1).TotalSeconds, internalClient.AccessTokenLifetime);
        Assert.True(internalClient.Enabled);
        var claim = Assert.Single(internalClient.Claims);
        Assert.Equal(JwtClaimTypes.Subject, claim.Type);
        Assert.Equal("blah", claim.Value);
    }
}
