using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IRecordManualRotationCommand" />
public class RecordManualRotationCommand : IRecordManualRotationCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IRotationScheduleCalculator _scheduleCalculator;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RecordManualRotationCommand(
        IPamRotationConfigRepository configRepository,
        IRotationScheduleCalculator scheduleCalculator,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _scheduleCalculator = scheduleCalculator;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task RecordAsync(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var details = await _configRepository.GetDetailsByIdAsync(configId);
        if (details is null || details.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (details.TargetSystemMethod != PamTargetSystemMethod.Manual)
        {
            throw new BadRequestException("This rotation config's target system is not manual.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var nextRotationAt = _scheduleCalculator.GetNextOccurrence(details.ScheduleCron, now);

        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.ManualRotationRecorded,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            CipherId = details.CipherId,
            RotationConfigId = details.Id,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        var toPersist = new PamRotationConfig
        {
            Id = details.Id,
            OrganizationId = details.OrganizationId,
            CipherId = details.CipherId,
            TargetSystemId = details.TargetSystemId,
            AccountIdentity = details.AccountIdentity,
            TerminateSessions = details.TerminateSessions,
            ScheduleCron = details.ScheduleCron,
            RotateOnAccessEnd = details.RotateOnAccessEnd,
            // Clears awaiting_manual_rotation: the obligation just discharged.
            NextRotationAt = nextRotationAt,
            Enabled = details.Enabled,
            LastRotationAt = now,
            CreationDate = details.CreationDate,
            RevisionDate = now,
        };
        await _configRepository.ReplaceAsync(toPersist);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
