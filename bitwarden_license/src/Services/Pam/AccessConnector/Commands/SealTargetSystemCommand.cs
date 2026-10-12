using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="ISealTargetSystemCommand" />
public class SealTargetSystemCommand : ISealTargetSystemCommand
{
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public SealTargetSystemCommand(
        IPamTargetSystemRepository targetSystemRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _targetSystemRepository = targetSystemRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task SealAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string sealedData)
    {
        if (string.IsNullOrWhiteSpace(sealedData))
        {
            throw new BadRequestException("Sealed data is required.");
        }

        var target = await _targetSystemRepository.GetByIdAsync(targetSystemId);
        if (target is null || target.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.TargetSystemPolicyUpdated,
            OccurredDate = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        target.SealedData = sealedData;
        // The only transition out of PendingSeal; a Disabled target stays Disabled.
        if (target.Status == PamTargetSystemStatus.PendingSeal)
        {
            target.Status = PamTargetSystemStatus.Active;
        }
        target.RevisionDate = now;
        await _targetSystemRepository.ReplaceAsync(target);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
