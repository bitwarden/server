#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

/// <summary>
/// The internal stale copy deletion's clients, <c>release.{service}</c>: a service of another platform that shares this
/// install's tenants, holding <see cref="GlobalSettings.StaleCopyDeletionClientSecret"/>.
/// </summary>
/// <remarks>
/// Client credentials only, for <see cref="ApiScopes.InternalStaleCopyDeletion"/> alone, so a token obtained this way
/// reaches the deletion and nothing else. A secret of its own, so no other internal client's secret deletes a tenant.
/// Short-lived: a quarter of an hour. Registered only when the secret is set.
/// </remarks>
internal class StaleCopyDeletionClientProvider : IClientProvider
{
    /// <summary>The prefix of the clients' identifiers.</summary>
    public const string Prefix = "release";

    private readonly GlobalSettings _globalSettings;

    public StaleCopyDeletionClientProvider(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public Task<Client?> GetAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.StaleCopyDeletionClientSecret))
        {
            return Task.FromResult<Client?>(null);
        }

        return Task.FromResult<Client?>(new Client
        {
            ClientId = $"{Prefix}.{identifier}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(_globalSettings.StaleCopyDeletionClientSecret.Sha256()) },
            AllowedScopes = [ApiScopes.InternalStaleCopyDeletion],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 900,
            Enabled = true,
            Claims =
            [
                new(JwtClaimTypes.Subject, identifier),
            ],
        });
    }
}
