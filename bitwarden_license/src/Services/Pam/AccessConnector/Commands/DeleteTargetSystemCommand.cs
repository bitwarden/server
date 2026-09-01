using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IDeleteTargetSystemCommand" />
public class DeleteTargetSystemCommand : IDeleteTargetSystemCommand
{
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public DeleteTargetSystemCommand(
        IPamTargetSystemRepository targetSystemRepository,
        IPamRotationConfigRepository configRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _targetSystemRepository = targetSystemRepository;
        _configRepository = configRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId)
    {
        var target = await _targetSystemRepository.GetByIdAsync(targetSystemId);
        if (target is null || target.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (await _configRepository.AnyByTargetSystemAsync(targetSystemId))
        {
            throw new BadRequestException(
                "This target system still has rotation configs. Delete them before deleting the target system.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // audit (before/after): the name is captured before the delete, since the durable record of it is this event
        // rather than the row.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.TargetSystemDeleted,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Cascades the target's access connector assignments in the same transaction. The repository re-checks the
        // rotation-config guard under lock, so a config created since the read above blocks the delete rather than
        // being left naming a target that no longer exists.
        if (!await _targetSystemRepository.DeleteWithAssignmentsAsync(targetSystemId))
        {
            throw new BadRequestException(
                "This target system still has rotation configs. Delete them before deleting the target system.");
        }

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
