#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

/// <summary>
/// The internal staff search's clients, <c>search.{service}</c>: a service of another platform that
/// shares this install's people, holding <see cref="GlobalSettings.StaffSearchClientSecret"/>.
/// </summary>
/// <remarks>
/// Client credentials only, for <see cref="ApiScopes.InternalStaffSearch"/> alone, so a token obtained
/// this way reaches the search and nothing else. A secret of its own, so a client of the account lookup
/// or deletion can't search. Short-lived: a quarter of an hour. Registered only when the secret is set.
/// </remarks>
internal class StaffSearchClientProvider : IClientProvider
{
    /// <summary>The prefix of the clients' identifiers.</summary>
    public const string Prefix = "search";

    private readonly GlobalSettings _globalSettings;

    public StaffSearchClientProvider(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public Task<Client?> GetAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.StaffSearchClientSecret))
        {
            return Task.FromResult<Client?>(null);
        }

        return Task.FromResult<Client?>(new Client
        {
            ClientId = $"{Prefix}.{identifier}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(_globalSettings.StaffSearchClientSecret.Sha256()) },
            AllowedScopes = [ApiScopes.InternalStaffSearch],
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
