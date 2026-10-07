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
/// Resolves the client-credentials <see cref="Client"/> for a PAM access connector, whose credential is a
/// <c>dbo.ApiKey</c> row as in <see cref="SecretsManagerApiKeyProvider"/>. The server never holds the plaintext org
/// key, only the ciphertext each token response carries.
/// </summary>
internal class PamAccessConnectorClientProvider : IClientProvider
{
    public const string AccessConnectorPrefix = "access-connector";

    /// <summary>
    /// Shorter than the one-hour default, bounding how long a disabled or deleted access connector or a lapsed license
    /// keeps a usable token. A polling access connector re-authenticates cheaply.
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
