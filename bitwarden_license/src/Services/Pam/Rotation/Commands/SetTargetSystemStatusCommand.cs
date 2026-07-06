using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="ISetTargetSystemStatusCommand" />
public class SetTargetSystemStatusCommand : ISetTargetSystemStatusCommand
{
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public SetTargetSystemStatusCommand(
        IPamTargetSystemRepository targetSystemRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _targetSystemRepository = targetSystemRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task SetStatusAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, bool enable)
    {
        var target = await _targetSystemRepository.GetByIdAsync(targetSystemId);
        if (target is null || target.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var desired = enable ? PamTargetSystemStatus.Active : PamTargetSystemStatus.Disabled;
        if (target.Status == desired)
        {
            throw new BadRequestException(enable
                ? "This target system is already enabled."
                : "This target system is already disabled.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var audit = new AccessAuditEventData
        {
            Kind = enable ? AccessAuditEventKind.TargetSystemEnabled : AccessAuditEventKind.TargetSystemDisabled,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        target.Status = desired;
        target.RevisionDate = now;
        await _targetSystemRepository.ReplaceAsync(target);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
