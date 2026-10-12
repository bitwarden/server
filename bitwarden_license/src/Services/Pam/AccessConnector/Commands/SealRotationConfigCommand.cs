using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="ISealRotationConfigCommand" />
public class SealRotationConfigCommand : ISealRotationConfigCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public SealRotationConfigCommand(
        IPamRotationConfigRepository configRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationConfig> SealAsync(
        Guid organizationId, Guid actingUserId, Guid configId, string sealedData)
    {
        if (string.IsNullOrWhiteSpace(sealedData))
        {
            throw new BadRequestException("Sealed data is required.");
        }

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

        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationAccountUpdated,
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
            ScheduleCron = details.ScheduleCron,
            RotateOnAccessEnd = details.RotateOnAccessEnd,
            NextRotationAt = details.NextRotationAt,
            Enabled = details.Enabled,
            LastRotationAt = details.LastRotationAt,
            CreationDate = details.CreationDate,
            RevisionDate = now,
        };
        await _configRepository.ReplaceAsync(toPersist);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        return toPersist;
    }
}
