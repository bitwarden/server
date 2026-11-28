using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.ClientProviders;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class CohortAssignmentSyncClientProviderTests
{
    [Fact]
    public async Task GetAsync_ReturnsAClientForTheSyncScopeAlone()
    {
        var sut = new CohortAssignmentSyncClientProvider(new GlobalSettings
        {
            CohortAssignmentSyncClientSecret = "a-secret",
            InternalIdentityKey = "another-secret",
        });

        var client = await sut.GetAsync("other-service");

        Assert.NotNull(client);
        Assert.Equal("cohorts.other-service", client.ClientId);
        Assert.True(client.RequireClientSecret);
        // Its own secret, not the internal client's.
        var secret = Assert.Single(client.ClientSecrets);
        Assert.Equal("a-secret".Sha256(), secret.Value);
        Assert.Equal(ApiScopes.InternalCohortAssignmentSync, Assert.Single(client.AllowedScopes));
        Assert.Equal(GrantTypes.ClientCredentials, client.AllowedGrantTypes);
        Assert.Equal(900, client.AccessTokenLifetime);
        Assert.True(client.Enabled);
        var claim = Assert.Single(client.Claims);
        Assert.Equal(JwtClaimTypes.Subject, claim.Type);
        Assert.Equal("other-service", claim.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAsync_NoSecret_ReturnsNoClient(string? secret)
    {
        var sut = new CohortAssignmentSyncClientProvider(new GlobalSettings
        {
            CohortAssignmentSyncClientSecret = secret!,
        });

        Assert.Null(await sut.GetAsync("other-service"));
    }

    [Fact]
    public void TheSyncScopeIsAnApiScopeAndAResource()
    {
        Assert.Equal("internal.cohort-assignment-sync", ApiScopes.InternalCohortAssignmentSync);
        Assert.Contains(ApiScopes.GetApiScopes(), scope => scope.Name == ApiScopes.InternalCohortAssignmentSync);
        Assert.Contains(Bit.Identity.IdentityServer.ApiResources.GetApiResources(),
            resource => resource.Name == ApiScopes.InternalCohortAssignmentSync);
    }
}
