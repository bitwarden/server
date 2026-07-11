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
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Services.Pam.Rotation.Models;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IRegisterDaemonCommand" />
public class RegisterDaemonCommand : IRegisterDaemonCommand
{
    /// <summary>The scope every daemon credential carries — mirrors Secrets Manager's access-token scope shape.</summary>
    private const string DaemonScope = "[\"api.pam.rotation\"]";
    private const int ClientSecretLength = 30;

    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RegisterDaemonCommand(
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository daemonRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _apiKeyRepository = apiKeyRepository;
        _daemonRepository = daemonRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<PamDaemonRegistrationResult> RegisterAsync(
        Guid organizationId, Guid actingUserId, string name, string encryptedPayload, string key)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // audit (before/after): record the registration attempt before either row is written, then the outcome once
        // both the credential and the daemon exist.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.DaemonRegistered,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            DaemonName = name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // The daemon's machine credential is a generic dbo.ApiKey row (ServiceAccountId null) -- PAM reuses the
        // Secrets Manager credential store rather than minting a parallel one. Hashing mirrors
        // CreateAccessTokenCommand exactly, since the same provider-side verification reads this hash.
        var clientSecret = CoreHelpers.SecureRandomString(ClientSecretLength);
        var apiKey = new ApiKey
        {
            ServiceAccountId = null,
            Name = name,
            ClientSecretHash = Hash(clientSecret),
            Scope = DaemonScope,
            EncryptedPayload = encryptedPayload,
            Key = key,
        };
        var createdApiKey = await _apiKeyRepository.CreateAsync(apiKey);

        var daemon = new PamDaemon
        {
            OrganizationId = organizationId,
            Name = name,
            ApiKeyId = createdApiKey.Id,
            Status = PamDaemonStatus.Enabled,
            CreationDate = now,
            RevisionDate = now,
        };
        var createdDaemon = await _daemonRepository.CreateAsync(daemon);

        await _accessAuditEventEmitter.EmitAsync(
            audit with { Phase = AccessAuditEventPhase.Outcome, DaemonId = createdDaemon.Id });

        // The plaintext client secret is surfaced exactly once -- the server never persists or logs it again.
        return new PamDaemonRegistrationResult(createdDaemon, clientSecret);
    }

    private static string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
