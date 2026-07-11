using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IDeleteDaemonCommand" />
public class DeleteDaemonCommand : IDeleteDaemonCommand
{
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public DeleteDaemonCommand(
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

    public async Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid daemonId)
    {
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);
        if (daemon is null || daemon.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // audit (before/after): record the attempt, then the outcome around the point of no return.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.DaemonDeleted,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            DaemonId = daemon.Id,
            DaemonName = daemon.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Remove the daemon first (PamDaemon_DeleteById clears its target assignments in the same transaction, since
        // that FK is ON DELETE NO ACTION), then delete its ApiKey credential. Order matters: the PamDaemon -> ApiKey
        // FK is also NO ACTION, so the referencing daemon row must be gone before the credential can be deleted.
        await _daemonRepository.DeleteAsync(daemon);

        var apiKey = await _apiKeyRepository.GetByIdAsync(daemon.ApiKeyId);
        if (apiKey is not null)
        {
            await _apiKeyRepository.DeleteAsync(apiKey);
        }

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
