using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IRenameTargetSystemCommand" />
public class RenameTargetSystemCommand : IRenameTargetSystemCommand
{
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RenameTargetSystemCommand(
        IPamTargetSystemRepository targetSystemRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _targetSystemRepository = targetSystemRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task RenameAsync(Guid organizationId, Guid actingUserId, Guid targetSystemId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required.");
        }

        var target = await _targetSystemRepository.GetByIdAsync(targetSystemId);
        if (target is null || target.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // The rename is display-only (the id keys the daemon's connector resolver); the prior name is preserved in
        // Detail since the target row itself will no longer carry it after the update.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.TargetSystemRenamed,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            TargetSystemId = target.Id,
            TargetSystemName = name,
            Detail = $"Renamed from '{target.Name}'.",
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        target.Name = name;
        target.RevisionDate = now;
        await _targetSystemRepository.ReplaceAsync(target);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
