using Bit.Core.Auth.IdentityServer;
using Bit.Identity.IdentityServer;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityServer.Models;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints;

public class ClientStoreCachingTests
{
    private const string ProbePrefix = "cache-probe";
    private const string ProbeSecret = "probe-secret";

    [Fact]
    public async Task TokenRequest_SameClientTwice_RunsClientProviderOnEachRequest()
    {
        var probeProvider = new CountingClientProvider();
        using var factory = new IdentityApplicationFactory();
        factory.ConfigureServices(services =>
            services.AddKeyedSingleton<IClientProvider>(ProbePrefix, probeProvider));

        var first = await RequestTokenAsync(factory);
        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        Assert.Equal(1, probeProvider.Calls);

        var second = await RequestTokenAsync(factory);
        Assert.Equal(StatusCodes.Status200OK, second.Response.StatusCode);
        // A cached client would leave the count at 1.
        Assert.Equal(2, probeProvider.Calls);
    }

    private static Task<HttpContext> RequestTokenAsync(IdentityApplicationFactory factory) =>
        factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"{ProbePrefix}.client" },
            { "client_secret", ProbeSecret },
            { "scope", ApiScopes.ApiSecrets },
        }));

    private sealed class CountingClientProvider : IClientProvider
    {
        private int _calls;

        public int Calls => _calls;

        public Task<Client?> GetAsync(string identifier)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult<Client?>(new Client
            {
                ClientId = $"{ProbePrefix}.{identifier}",
                ClientSecrets = { new Secret(ProbeSecret.Sha256()) },
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                // A client with this scope needs no user, so the token request succeeds.
                AllowedScopes = { ApiScopes.ApiSecrets },
            });
        }
    }
}
