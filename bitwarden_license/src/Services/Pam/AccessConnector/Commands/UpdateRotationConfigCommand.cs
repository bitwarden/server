using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IUpdateRotationConfigCommand" />
public class UpdateRotationConfigCommand : IUpdateRotationConfigCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IRotationScheduleCalculator _scheduleCalculator;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public UpdateRotationConfigCommand(
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
        Guid organizationId,
        Guid actingUserId,
        Guid configId,
        string sealedData,
        string wrappedKey,
        string? scheduleCron,
        bool rotateOnAccessEnd)
    {
        if (string.IsNullOrWhiteSpace(sealedData))
        {
            throw new BadRequestException("Sealed data is required.");
        }

        if (string.IsNullOrWhiteSpace(wrappedKey))
        {
            throw new BadRequestException("Wrapped key is required.");
        }

        var details = await _configRepository.GetDetailsByIdAsync(configId);
        if (details is null || details.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (details.HasActiveJob && (sealedData != details.SealedData || wrappedKey != details.WrappedKey))
        {
            throw new BadRequestException("This rotation config has an active job.");
        }

        _scheduleCalculator.ValidateSchedule(scheduleCron, _options.Value.MinScheduleInterval);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationConfigUpdated,
            OccurredDate = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            CipherId = details.CipherId,
            RotationConfigId = details.Id,
            TargetSystemId = details.TargetSystemId,
            TargetSystemName = details.TargetSystemName,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // A plain PamRotationConfig, since ReplaceAsync must not forward the details projection's display-only
        // properties.
        var toPersist = new PamRotationConfig
        {
            Id = details.Id,
            OrganizationId = details.OrganizationId,
            CipherId = details.CipherId,
            TargetSystemId = details.TargetSystemId,
            SealedData = sealedData,
            WrappedKey = wrappedKey,
            ScheduleCron = scheduleCron,
            RotateOnAccessEnd = rotateOnAccessEnd,
            NextRotationDate = _scheduleCalculator.GetNextOccurrence(scheduleCron, now),
            Enabled = details.Enabled,
            LastRotationDate = details.LastRotationDate,
            CreationDate = details.CreationDate,
            RevisionDate = now,
        };
        await _configRepository.ReplaceAsync(toPersist);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        return toPersist;
    }
}
