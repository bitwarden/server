using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IResumeRotationCommand" />
public class ResumeRotationCommand : IResumeRotationCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IRotationScheduleCalculator _scheduleCalculator;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public ResumeRotationCommand(
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

    public async Task ResumeAsync(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var details = await _configRepository.GetDetailsByIdAsync(configId);
        if (details is null || details.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (details.Enabled)
        {
            throw new BadRequestException("This rotation config is already active.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // A manual-target config with a due obligation, read while paused, has that obligation pulled due rather
        // than pushed further out by recomputing from the schedule.
        var nextRotationAt =
            details.TargetSystemMethod == PamTargetSystemMethod.Manual
            && details.NextRotationAt is { } nextRotationDue && nextRotationDue <= now
                ? now
                : _scheduleCalculator.GetNextOccurrence(details.ScheduleCron, now);

        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationResumed,
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
            NextRotationAt = nextRotationAt,
            Enabled = true,
            LastRotationAt = details.LastRotationAt,
            CreationDate = details.CreationDate,
            RevisionDate = now,
        };
        await _configRepository.ReplaceAsync(toPersist);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
