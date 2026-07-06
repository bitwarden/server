using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IDeleteRotationConfigCommand" />
public class DeleteRotationConfigCommand : IDeleteRotationConfigCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public DeleteRotationConfigCommand(
        IPamRotationConfigRepository configRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var details = await _configRepository.GetDetailsByIdAsync(configId);
        if (details is null || details.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (details.HasActiveJob)
        {
            throw new BadRequestException("This rotation config has an active job.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // audit (before/after): names are captured before the delete, since the durable record of them is this
        // event, not the row.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationConfigDeleted,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            CipherId = details.CipherId,
            RotationConfigId = details.Id,
            TargetSystemId = details.TargetSystemId,
            TargetSystemName = details.TargetSystemName,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Cascades the config's jobs and attempts in the same transaction -- the audit trail above is the durable
        // history, not these rows.
        await _configRepository.DeleteWithJobsAsync(configId);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
