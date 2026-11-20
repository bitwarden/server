using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.ClientProviders;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class AccountLookupClientProviderTests
{
    [Fact]
    public async Task GetAsync_ReturnsAClientForTheLookupScopeAlone()
    {
        var sut = new AccountLookupClientProvider(new GlobalSettings { AccountLookupClientSecret = "a-secret" });

        var client = await sut.GetAsync("internal-service");

        Assert.NotNull(client);
        Assert.Equal("lookup.internal-service", client.ClientId);
        Assert.True(client.RequireClientSecret);
        var secret = Assert.Single(client.ClientSecrets);
        Assert.Equal("a-secret".Sha256(), secret.Value);
        Assert.Equal(ApiScopes.InternalAccountLookup, Assert.Single(client.AllowedScopes));
        Assert.Equal(GrantTypes.ClientCredentials, client.AllowedGrantTypes);
        Assert.Equal(900, client.AccessTokenLifetime);
        Assert.True(client.Enabled);
        var claim = Assert.Single(client.Claims);
        Assert.Equal(JwtClaimTypes.Subject, claim.Type);
        Assert.Equal("internal-service", claim.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAsync_NoSecret_ReturnsNoClient(string? secret)
    {
        var sut = new AccountLookupClientProvider(new GlobalSettings { AccountLookupClientSecret = secret! });

        Assert.Null(await sut.GetAsync("internal-service"));
    }

    [Fact]
    public void TheLookupScopeIsAnApiScopeAndAResource()
    {
        Assert.Contains(ApiScopes.GetApiScopes(), scope => scope.Name == ApiScopes.InternalAccountLookup);
        Assert.Contains(Bit.Identity.IdentityServer.ApiResources.GetApiResources(),
            resource => resource.Name == ApiScopes.InternalAccountLookup);
    }
}
