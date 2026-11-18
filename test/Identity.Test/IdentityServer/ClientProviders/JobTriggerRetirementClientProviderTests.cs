using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer.ClientProviders;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class JobTriggerRetirementClientProviderTests
{
    [Fact]
    public async Task GetAsync_ReturnsAClientForTheRetirementScopeAlone()
    {
        var sut = new JobTriggerRetirementClientProvider(new GlobalSettings
        {
            JobTriggerRetirementClientSecret = "a-secret",
            InternalIdentityKey = "another-secret",
        });

        var client = await sut.GetAsync("migration-service");

        Assert.NotNull(client);
        Assert.Equal("retire.migration-service", client.ClientId);
        Assert.True(client.RequireClientSecret);
        // Its own secret, not the internal client's.
        var secret = Assert.Single(client.ClientSecrets);
        Assert.Equal("a-secret".Sha256(), secret.Value);
        Assert.Equal(ApiScopes.InternalJobTriggerRetirement, Assert.Single(client.AllowedScopes));
        Assert.Equal(GrantTypes.ClientCredentials, client.AllowedGrantTypes);
        Assert.Equal(900, client.AccessTokenLifetime);
        Assert.True(client.Enabled);
        var claim = Assert.Single(client.Claims);
        Assert.Equal(JwtClaimTypes.Subject, claim.Type);
        Assert.Equal("migration-service", claim.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAsync_NoSecret_ReturnsNoClient(string? secret)
    {
        var sut = new JobTriggerRetirementClientProvider(new GlobalSettings
        {
            JobTriggerRetirementClientSecret = secret!,
        });

        Assert.Null(await sut.GetAsync("migration-service"));
    }

    [Fact]
    public void TheRetirementScopeIsAnApiScopeAndAResource()
    {
        Assert.Equal("internal.job-trigger-retirement", ApiScopes.InternalJobTriggerRetirement);
        Assert.Contains(ApiScopes.GetApiScopes(), scope => scope.Name == ApiScopes.InternalJobTriggerRetirement);
        Assert.Contains(Bit.Identity.IdentityServer.ApiResources.GetApiResources(),
            resource => resource.Name == ApiScopes.InternalJobTriggerRetirement);
    }
}
