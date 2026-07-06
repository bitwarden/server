using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IUpdateRotationSettingsCommand" />
public class UpdateRotationSettingsCommand : IUpdateRotationSettingsCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IRotationScheduleCalculator _scheduleCalculator;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public UpdateRotationSettingsCommand(
        IPamRotationConfigRepository configRepository,
        IRotationScheduleCalculator scheduleCalculator,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _scheduleCalculator = scheduleCalculator;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationConfig> UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid configId, string? scheduleCron, bool rotateOnAccessEnd)
    {
        var config = await _configRepository.GetByIdAsync(configId);
        if (config is null || config.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        _scheduleCalculator.ValidateSchedule(scheduleCron, _options.Value.MinScheduleInterval);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationSettingsUpdated,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            CipherId = config.CipherId,
            RotationConfigId = config.Id,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Recompute-on-edit: a new cron re-derives NextRotationAt from now; a cleared cron clears it.
        config.ScheduleCron = scheduleCron;
        config.RotateOnAccessEnd = rotateOnAccessEnd;
        config.NextRotationAt = _scheduleCalculator.GetNextOccurrence(scheduleCron, now);
        config.RevisionDate = now;
        await _configRepository.ReplaceAsync(config);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        return config;
    }
}
