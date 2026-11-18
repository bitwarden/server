#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

/// <summary>
/// The internal account lookup's clients, <c>lookup.{service}</c>: a service of another platform that
/// shares this install's people, holding <see cref="GlobalSettings.AccountLookupClientSecret"/>.
/// </summary>
/// <remarks>
/// Client credentials only, for <see cref="ApiScopes.InternalAccountLookup"/> alone, so a token
/// obtained this way reaches the lookup and nothing else. Short-lived: a quarter of an hour.
/// Registered only when the secret is set.
/// </remarks>
internal class AccountLookupClientProvider : IClientProvider
{
    /// <summary>The prefix of the clients' identifiers.</summary>
    public const string Prefix = "lookup";

    private readonly GlobalSettings _globalSettings;

    public AccountLookupClientProvider(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public Task<Client?> GetAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.AccountLookupClientSecret))
        {
            return Task.FromResult<Client?>(null);
        }

        return Task.FromResult<Client?>(new Client
        {
            ClientId = $"{Prefix}.{identifier}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(_globalSettings.AccountLookupClientSecret.Sha256()) },
            AllowedScopes = [ApiScopes.InternalAccountLookup],
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
