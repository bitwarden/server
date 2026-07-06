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
/// Resolves the OAuth client-credentials <see cref="Client"/> for a PAM rotation daemon. The daemon's machine
/// credential is a generic <c>dbo.ApiKey</c> row (mirrors Secrets Manager's machine-account mechanic in
/// <see cref="SecretsManagerApiKeyProvider"/>) with a null <c>ServiceAccountId</c>; the owner link is inverted via
/// <c>PamDaemon.ApiKeyId</c>. Authentication is denied unless the daemon is Enrolled and its organization has PAM
/// enabled and licensed — the server never holds the daemon's plaintext org key, only the ciphertext
/// <c>EncryptedPayload</c> handed back on every token response (zero-knowledge; see
/// <see cref="Duende.IdentityServer.Models.Client.Properties"/> "encryptedPayload").
/// </summary>
internal class PamDaemonClientProvider : IClientProvider
{
    public const string DaemonPrefix = "daemon";

    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IPamDaemonRepository _pamDaemonRepository;

    public PamDaemonClientProvider(IApiKeyRepository apiKeyRepository, IPamDaemonRepository pamDaemonRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _pamDaemonRepository = pamDaemonRepository;
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

        var daemonDetails = await _pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKeyId);
        if (daemonDetails == null
            || daemonDetails.Status != PamDaemonStatus.Enrolled
            || !daemonDetails.OrganizationEnabled
            || !daemonDetails.OrganizationUsePam)
        {
            return null;
        }

        return new Client
        {
            ClientId = $"{DaemonPrefix}.{apiKeyId}",
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
                new(JwtClaimTypes.Subject, daemonDetails.Id.ToString()),
                new(Claims.Type, IdentityClientType.RotationDaemon.ToString()),
                new(Claims.Organization, daemonDetails.OrganizationId.ToString()),
            },
        };
    }
}
