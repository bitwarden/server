using System.Security.Cryptography;
using System.Text;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IRegisterAccessConnectorCommand" />
public class RegisterAccessConnectorCommand : IRegisterAccessConnectorCommand
{
    /// <summary>The scope every access connector credential carries — mirrors Secrets Manager's access-token scope shape.</summary>
    private const string AccessConnectorScope = "[\"api.pam.rotation\"]";
    private const int ClientSecretLength = 30;

    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RegisterAccessConnectorCommand(
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository accessConnectorRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _apiKeyRepository = apiKeyRepository;
        _accessConnectorRepository = accessConnectorRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<PamAccessConnectorRegistrationResult> RegisterAsync(
        Guid organizationId, Guid actingUserId, string name, string encryptedPayload, string key)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Records the registration attempt before either row is written, then the outcome after both exist.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.AccessConnectorRegistered,
            OccurredDate = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            AccessConnectorName = name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // The access connector's machine credential is a generic dbo.ApiKey row (ServiceAccountId null), reusing the
        // Secrets Manager credential store. Hashing mirrors CreateAccessTokenCommand, since the same provider-side
        // verification reads this hash.
        var clientSecret = CoreHelpers.SecureRandomString(ClientSecretLength);
        var apiKey = new ApiKey
        {
            ServiceAccountId = null,
            Name = name,
            ClientSecretHash = Hash(clientSecret),
            Scope = AccessConnectorScope,
            EncryptedPayload = encryptedPayload,
            Key = key,
        };
        var createdApiKey = await _apiKeyRepository.CreateAsync(apiKey);

        var accessConnector = new PamAccessConnector
        {
            OrganizationId = organizationId,
            Name = name,
            ApiKeyId = createdApiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
            CreationDate = now,
            RevisionDate = now,
        };
        var createdAccessConnector = await _accessConnectorRepository.CreateAsync(accessConnector);

        await _accessAuditEventEmitter.EmitAsync(
            audit with { Phase = AccessAuditEventPhase.Outcome, AccessConnectorId = createdAccessConnector.Id });

        // The plaintext client secret is surfaced here only; the server never persists or logs it again.
        return new PamAccessConnectorRegistrationResult(createdAccessConnector, clientSecret);
    }

    private static string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
