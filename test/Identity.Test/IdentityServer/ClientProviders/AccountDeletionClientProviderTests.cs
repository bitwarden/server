using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.ClientProviders;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class AccountDeletionClientProviderTests
{
    [Fact]
    public async Task GetAsync_ReturnsAClientForTheDeletionScopeAlone()
    {
        var sut = new AccountDeletionClientProvider(new GlobalSettings
        {
            AccountDeletionClientSecret = "a-secret",
            InternalIdentityKey = "another-secret",
        });

        var client = await sut.GetAsync("a-service");

        Assert.NotNull(client);
        Assert.Equal("deletion.a-service", client.ClientId);
        Assert.True(client.RequireClientSecret);
        // Its own secret, not the internal client's.
        var secret = Assert.Single(client.ClientSecrets);
        Assert.Equal("a-secret".Sha256(), secret.Value);
        Assert.Equal(ApiScopes.InternalAccountDeletion, Assert.Single(client.AllowedScopes));
        Assert.Equal(GrantTypes.ClientCredentials, client.AllowedGrantTypes);
        Assert.Equal(900, client.AccessTokenLifetime);
        Assert.True(client.Enabled);
        var claim = Assert.Single(client.Claims);
        Assert.Equal(JwtClaimTypes.Subject, claim.Type);
        Assert.Equal("a-service", claim.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAsync_NoSecret_ReturnsNoClient(string? secret)
    {
        var sut = new AccountDeletionClientProvider(new GlobalSettings
        {
            AccountDeletionClientSecret = secret!,
        });

        Assert.Null(await sut.GetAsync("a-service"));
    }

    [Fact]
    public void TheDeletionScopeIsAnApiScopeAndAResource()
    {
        Assert.Contains(ApiScopes.GetApiScopes(), scope => scope.Name == ApiScopes.InternalAccountDeletion);
        Assert.Contains(Bit.Identity.IdentityServer.ApiResources.GetApiResources(),
            resource => resource.Name == ApiScopes.InternalAccountDeletion);
    }
}
