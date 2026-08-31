using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IDeleteAccessConnectorCommand" />
public class DeleteAccessConnectorCommand : IDeleteAccessConnectorCommand
{
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public DeleteAccessConnectorCommand(
        IPamDaemonRepository daemonRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _daemonRepository = daemonRepository;
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

        await _daemonRepository.DeleteAsync(daemon);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
