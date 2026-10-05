// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Identity;
using Bit.Core.SecretsManager.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

/// <summary>
/// Resolves the OAuth client-credentials <see cref="Client"/> for a PAM access connector. The access connector's
/// machine credential is a generic <c>dbo.ApiKey</c> row (mirrors Secrets Manager's machine-account mechanic in
/// <see cref="SecretsManagerApiKeyProvider"/>) with a null <c>ServiceAccountId</c>, owner-linked via
/// <c>PamAccessConnector.ApiKeyId</c>. Authentication is denied unless the access connector is Enabled and its
/// organization has PAM enabled and licensed. The access token's lifetime is shorter than the platform default, so an
/// already-issued token outlives a disable, delete, or license lapse by minutes rather than an hour. The server never
/// holds the access connector's plaintext org key, only the ciphertext handed back on every token response.
/// </summary>
internal class PamAccessConnectorClientProvider : IClientProvider
{
    public const string AccessConnectorPrefix = "access-connector";

    /// <summary>
    /// How long an access connector's access token stays valid. Shorter than the one-hour platform default: an access
    /// connector polls continuously, so it re-authenticates cheaply, and the shorter window bounds how long a revoked
    /// access connector or a lapsed PAM license keeps a usable token.
    /// </summary>
    private const int AccessConnectorAccessTokenLifetimeInMinutes = 15;

    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IPamAccessConnectorRepository _pamAccessConnectorRepository;

    public PamAccessConnectorClientProvider(
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _pamAccessConnectorRepository = pamAccessConnectorRepository;
    }

    public async Task<Client> GetAsync(string identifier)
    {
        if (!Guid.TryParse(identifier, out var apiKeyId))
        {
            return null;
        }

        var apiKey = await _apiKeyRepository.GetByIdAsync(apiKeyId);
        if (apiKey == null || apiKey.ExpireAt <= DateTime.UtcNow)
        {
            return null;
        }

        var accessConnectorDetails = await _pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKeyId);
        if (accessConnectorDetails == null
            || accessConnectorDetails.Status != PamAccessConnectorStatus.Enabled
            || !accessConnectorDetails.OrganizationEnabled
            || !accessConnectorDetails.OrganizationUsePam)
        {
            return null;
        }

        return new Client
        {
            ClientId = $"{AccessConnectorPrefix}.{apiKeyId}",
            RequireClientSecret = true,
            ClientSecrets = { new Secret(apiKey.ClientSecretHash) },
            AllowedScopes = apiKey.GetScopes(),
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 60 * AccessConnectorAccessTokenLifetimeInMinutes,
            ClientClaimsPrefix = null,
            Properties = new Dictionary<string, string> {
                {"encryptedPayload", apiKey.EncryptedPayload},
            },
            Claims = new List<ClientClaim>
            {
                new(JwtClaimTypes.Subject, accessConnectorDetails.Id.ToString()),
                new(Claims.Type, IdentityClientType.AccessConnector.ToString()),
                new(Claims.Organization, accessConnectorDetails.OrganizationId.ToString()),
            },
        };
    }
}
