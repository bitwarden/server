using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IUnassignDaemonFromTargetCommand" />
public class UnassignDaemonFromTargetCommand : IUnassignDaemonFromTargetCommand
{
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public UnassignDaemonFromTargetCommand(
        IPamDaemonRepository daemonRepository,
        IPamTargetSystemRepository targetSystemRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _daemonRepository = daemonRepository;
        _targetSystemRepository = targetSystemRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task UnassignAsync(Guid organizationId, Guid actingUserId, Guid daemonId, Guid targetSystemId)
    {
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);
        if (daemon is null || daemon.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var target = await _targetSystemRepository.GetByIdAsync(targetSystemId);
        if (target is null || target.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (!await _daemonRepository.AssignmentExistsAsync(daemonId, targetSystemId))
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.DaemonUnassignedFromTarget,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            DaemonId = daemon.Id,
            DaemonName = daemon.Name,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        await _daemonRepository.DeleteAssignmentAsync(daemonId, targetSystemId);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
