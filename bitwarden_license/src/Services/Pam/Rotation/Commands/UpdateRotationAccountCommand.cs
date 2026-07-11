using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IUpdateRotationAccountCommand" />
public class UpdateRotationAccountCommand : IUpdateRotationAccountCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public UpdateRotationAccountCommand(
        IPamRotationConfigRepository configRepository,
        IPamTargetSystemRepository targetSystemRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _targetSystemRepository = targetSystemRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationConfig> UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid configId, string accountIdentity, bool terminateSessions)
    {
        if (string.IsNullOrWhiteSpace(accountIdentity))
        {
            throw new BadRequestException("Account identity is required.");
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

        var target = await _targetSystemRepository.GetByIdAsync(details.TargetSystemId);
        if (target is null)
        {
            throw new NotFoundException();
        }

        // Same termination-capability guard as create: only an automatic target reporting the capability may have
        // TerminateSessions set.
        if (terminateSessions &&
            !(details.TargetSystemMethod == PamTargetSystemMethod.Automatic && target.SupportsSessionTermination == true))
        {
            throw new BadRequestException(
                "Session termination requires an automatic target system that supports it.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationAccountUpdated,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            CipherId = details.CipherId,
            RotationConfigId = details.Id,
            TargetSystemId = details.TargetSystemId,
            TargetSystemName = details.TargetSystemName,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Persist a plain PamRotationConfig: the PamRotationConfigDetails projection carries extra display-only
        // properties (TargetSystemName, TargetSystemMethod, HasActiveJob) that the base ReplaceAsync would otherwise
        // forward.
        var toPersist = new PamRotationConfig
        {
            Id = details.Id,
            OrganizationId = details.OrganizationId,
            CipherId = details.CipherId,
            TargetSystemId = details.TargetSystemId,
            AccountIdentity = accountIdentity,
            TerminateSessions = terminateSessions,
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
