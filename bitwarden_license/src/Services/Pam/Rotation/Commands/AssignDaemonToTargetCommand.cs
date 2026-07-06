using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IAssignDaemonToTargetCommand" />
public class AssignDaemonToTargetCommand : IAssignDaemonToTargetCommand
{
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public AssignDaemonToTargetCommand(
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

    public async Task AssignAsync(Guid organizationId, Guid actingUserId, Guid daemonId, Guid targetSystemId)
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

        // Both rows were just loaded against the same route organization, so daemon.OrganizationId ==
        // target.OrganizationId == organizationId holds by construction (the cross-cutting same-org invariant).
        if (daemon.Status != PamDaemonStatus.Enrolled)
        {
            throw new BadRequestException("This daemon has been revoked.");
        }

        if (target.Method != PamTargetSystemMethod.Automatic)
        {
            throw new BadRequestException("Only automatic target systems can be assigned a daemon.");
        }

        if (await _daemonRepository.AssignmentExistsAsync(daemonId, targetSystemId))
        {
            throw new BadRequestException("This daemon is already assigned to this target system.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var assignment = new PamDaemonTargetAssignment
        {
            DaemonId = daemonId,
            TargetSystemId = targetSystemId,
            OrganizationId = organizationId,
            CreationDate = now,
        };
        // CreateAssignmentAsync is a guarded custom insert, not the generic single-object CreateAsync -- it expects
        // the id to already be assigned.
        assignment.SetNewId();

        // audit (before/after): both names are snapshotted here (the commands hold the entities) rather than joined
        // on read.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.DaemonAssignedToTarget,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            DaemonId = daemon.Id,
            DaemonName = daemon.Name,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        await _daemonRepository.CreateAssignmentAsync(assignment);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
