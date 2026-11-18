#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Settings;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

/// <summary>
/// The internal job trigger retirement's clients, <c>retire.{service}</c>: internal clients holding
/// <see cref="GlobalSettings.JobTriggerRetirementClientSecret"/>.
/// </summary>
/// <remarks>
/// Client credentials only, for <see cref="ApiScopes.InternalJobTriggerRetirement"/> alone, so a token obtained this
/// way reaches the retirement and nothing else. A secret of its own, so no other internal client's secret deletes
/// job triggers. Short-lived: a quarter of an hour. Registered only when the secret is set.
/// </remarks>
internal class JobTriggerRetirementClientProvider : IClientProvider
{
    /// <summary>The prefix of the clients' identifiers.</summary>
    public const string Prefix = "retire";

    private readonly GlobalSettings _globalSettings;

    public JobTriggerRetirementClientProvider(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public Task<Client?> GetAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.JobTriggerRetirementClientSecret))
        {
            return Task.FromResult<Client?>(null);
        }

        return Task.FromResult<Client?>(new Client
        {
            ClientId = $"{Prefix}.{identifier}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(_globalSettings.JobTriggerRetirementClientSecret.Sha256()) },
            AllowedScopes = [ApiScopes.InternalJobTriggerRetirement],
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
