// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Identity;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

internal class SecretsManagerApiKeyProvider : IClientProvider
{
    public const string ApiKeyPrefix = "apikey";

    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public SecretsManagerApiKeyProvider(IApiKeyRepository apiKeyRepository, IOrganizationRepository organizationRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _organizationRepository = organizationRepository;
    }

    public async Task<Client> GetAsync(string identifier)
    {
        if (!Guid.TryParse(identifier, out var apiKeyId))
        {
            return null;
        }

        var apiKey = await _apiKeyRepository.GetDetailsByIdAsync(apiKeyId);

        if (apiKey == null || apiKey.ExpireAt <= DateTime.UtcNow)
        {
            return null;
        }

        switch (apiKey)
        {
            // ApiKeyRepository always materializes ServiceAccountApiKeyDetails and ApiKeyDetailsView LEFT JOINs
            // ServiceAccount, so a machine credential that is not a service account's -- a PAM rotation daemon's,
            // for instance -- arrives here with ServiceAccountOrganizationId defaulted and no organization to load.
            // Match on the service-account id rather than the type, and refuse anything else: those credentials
            // belong to their own provider, reached under a different client-id prefix.
            case ServiceAccountApiKeyDetails { ServiceAccountId: not null } key:
                var org = await _organizationRepository.GetByIdAsync(key.ServiceAccountOrganizationId);
                if (org == null || !org.UseSecretsManager || !org.Enabled)
                {
                    return null;
                }
                break;
            default:
                return null;
        }

        var client = new Client
        {
            ClientId = identifier,
            RequireClientSecret = true,
            ClientSecrets = { new Secret(apiKey.ClientSecretHash) },
            AllowedScopes = apiKey.GetScopes(),
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 3600 * 1,
            ClientClaimsPrefix = null,
            Properties = new Dictionary<string, string> {
                {"encryptedPayload", apiKey.EncryptedPayload},
            },
            Claims = new List<ClientClaim>
            {
                new(JwtClaimTypes.Subject, apiKey.ServiceAccountId.ToString()),
                new(Claims.Type, IdentityClientType.ServiceAccount.ToString()),
            },
        };

        switch (apiKey)
        {
            case ServiceAccountApiKeyDetails key:
                client.Claims.Add(new ClientClaim(Claims.Organization, key.ServiceAccountOrganizationId.ToString()));
                break;
        }

        return client;
    }
}
