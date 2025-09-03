#nullable enable

using System.Diagnostics;
using Bit.Core.IdentityServer;
using Bit.Core.Settings;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

internal class InternalClientProvider : IClientProvider
{
    private readonly GlobalSettings _globalSettings;

    public InternalClientProvider(GlobalSettings globalSettings)
    {
        // This class should not have been registered when it's not self hosted
        Debug.Assert(globalSettings.SelfHosted);

        _globalSettings = globalSettings;
    }

    public Task<Client?> GetAsync(string identifier)
    {
        return Task.FromResult<Client?>(new Client
        {
            ClientId = $"internal.{identifier}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(_globalSettings.InternalIdentityKey.Sha256()) },
            AllowedScopes = [ApiScopes.Internal],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 24,
            Enabled = true,
            Claims =
            [
                new(JwtClaimTypes.Subject, identifier),
            ],
        });
    }
}
