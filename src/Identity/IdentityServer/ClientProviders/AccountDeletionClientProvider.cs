#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

/// <summary>
/// The internal account deletion's clients, <c>deletion.{service}</c>: a service of another platform
/// that shares this install's people, holding <see cref="GlobalSettings.AccountDeletionClientSecret"/>.
/// </summary>
/// <remarks>
/// Client credentials only, for <see cref="ApiScopes.InternalAccountDeletion"/> alone, so a token
/// obtained this way reaches the deletion and nothing else. A secret of its own, so no other internal
/// client's secret deletes an account. Short-lived: a quarter of an hour. Registered only when the secret is
/// set.
/// </remarks>
internal class AccountDeletionClientProvider : IClientProvider
{
    /// <summary>The prefix of the clients' identifiers.</summary>
    public const string Prefix = "deletion";

    private readonly GlobalSettings _globalSettings;

    public AccountDeletionClientProvider(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public Task<Client?> GetAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.AccountDeletionClientSecret))
        {
            return Task.FromResult<Client?>(null);
        }

        return Task.FromResult<Client?>(new Client
        {
            ClientId = $"{Prefix}.{identifier}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(_globalSettings.AccountDeletionClientSecret.Sha256()) },
            AllowedScopes = [ApiScopes.InternalAccountDeletion],
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
