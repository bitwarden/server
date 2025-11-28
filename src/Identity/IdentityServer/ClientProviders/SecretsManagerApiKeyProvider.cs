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
            case ServiceAccountApiKeyDetails key:
                var org = await _organizationRepository.GetByIdAsync(key.ServiceAccountOrganizationId);
                if (!org.UseSecretsManager || !org.Enabled)
                {
                    return null;
                }
                break;
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
