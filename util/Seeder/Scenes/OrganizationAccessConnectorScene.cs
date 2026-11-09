using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Registers a PAM access connector for an organization: a credential <see cref="ApiKey"/> (no service account,
/// scope <c>api.pam.rotation</c>) holding the caller-supplied encrypted payload and key, plus the
/// <see cref="PamAccessConnector"/> that references it. Mirrors <c>RegisterAccessConnectorCommand</c>. The
/// client secret is returned in plaintext; the caller assembles the access token from it.
/// </summary>
public class OrganizationAccessConnectorScene(
    IOrganizationRepository organizationRepository,
    IApiKeyRepository apiKeyRepository,
    IPamAccessConnectorRepository accessConnectorRepository,
    IManglerService manglerService) : IScene<OrganizationAccessConnectorScene.Request, OrganizationAccessConnectorScene.Result>
{
    private const string _accessConnectorScope = "[\"api.pam.rotation\"]";
    private const int _clientSecretLength = 30;
    private const string _apiKeyName = "PAM access connector";

    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string Name { get; set; }
        /// <summary>EncString minted by the SDK for the access connector token; stored verbatim.</summary>
        [Required]
        public required string EncryptedPayload { get; set; }
        /// <summary>EncString minted by the SDK for the access connector token; stored verbatim.</summary>
        [Required]
        public required string Key { get; set; }
        public PamAccessConnectorStatus Status { get; set; } = PamAccessConnectorStatus.Enabled;
    }

    public class Result
    {
        public required Guid AccessConnectorId { get; init; }
        public required Guid ApiKeyId { get; init; }
        public required string ClientSecret { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        if (await organizationRepository.GetByIdAsync(request.OrganizationId) == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        var clientSecret = CoreHelpers.SecureRandomString(_clientSecretLength);
        var apiKey = await apiKeyRepository.CreateAsync(new ApiKey
        {
            ServiceAccountId = null,
            Name = _apiKeyName,
            ClientSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret))),
            Scope = _accessConnectorScope,
            EncryptedPayload = request.EncryptedPayload,
            Key = request.Key
        });

        var accessConnector = await accessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name,
            ApiKeyId = apiKey.Id,
            Status = request.Status
        });

        return new SceneResult<Result>(
            result: new Result
            {
                AccessConnectorId = accessConnector.Id,
                ApiKeyId = apiKey.Id,
                ClientSecret = clientSecret
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
