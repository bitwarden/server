#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

/// <summary>
/// The internal cohort assignment sync's clients, <c>cohorts.{service}</c>: a service of another platform that
/// shares this install's tenants, holding <see cref="GlobalSettings.CohortAssignmentSyncClientSecret"/>.
/// </summary>
/// <remarks>
/// Client credentials only, for <see cref="ApiScopes.InternalCohortAssignmentSync"/> alone, so a token obtained this
/// way reaches the sync and nothing else. A secret of its own, so no other internal client's secret
/// writes cohort assignments. Short-lived: a quarter of an hour. Registered only when the secret is set.
/// </remarks>
internal class CohortAssignmentSyncClientProvider : IClientProvider
{
    /// <summary>The prefix of the clients' identifiers.</summary>
    public const string Prefix = "cohorts";

    private readonly GlobalSettings _globalSettings;

    public CohortAssignmentSyncClientProvider(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public Task<Client?> GetAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.CohortAssignmentSyncClientSecret))
        {
            return Task.FromResult<Client?>(null);
        }

        return Task.FromResult<Client?>(new Client
        {
            ClientId = $"{Prefix}.{identifier}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(_globalSettings.CohortAssignmentSyncClientSecret.Sha256()) },
            AllowedScopes = [ApiScopes.InternalCohortAssignmentSync],
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
