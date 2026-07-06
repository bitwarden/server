using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IRevokeDaemonCommand" />
public class RevokeDaemonCommand : IRevokeDaemonCommand
{
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RevokeDaemonCommand(
        IPamDaemonRepository daemonRepository,
        IApiKeyRepository apiKeyRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _daemonRepository = daemonRepository;
        _apiKeyRepository = apiKeyRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task RevokeAsync(Guid organizationId, Guid actingUserId, Guid daemonId)
    {
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);
        if (daemon is null || daemon.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (daemon.Status != PamDaemonStatus.Enrolled)
        {
            throw new BadRequestException("This daemon has already been revoked.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // audit (before/after): record the revoke attempt, then the outcome around the point of no return.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.DaemonRevoked,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            DaemonId = daemon.Id,
            DaemonName = daemon.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        daemon.Status = PamDaemonStatus.Revoked;
        daemon.RevisionDate = now;
        await _daemonRepository.ReplaceAsync(daemon);

        // Delete the credential itself (SM's revocation semantics) -- the daemon row stays, since assignments and
        // the audit trail reference it, and re-enrollment (the deferred ReissueDaemonCredential) mints a new one.
        var apiKey = await _apiKeyRepository.GetByIdAsync(daemon.ApiKeyId);
        if (apiKey is not null)
        {
            await _apiKeyRepository.DeleteAsync(apiKey);
        }

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
