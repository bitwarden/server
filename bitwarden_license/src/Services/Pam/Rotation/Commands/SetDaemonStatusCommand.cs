using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="ISetDaemonStatusCommand" />
public class SetDaemonStatusCommand : ISetDaemonStatusCommand
{
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public SetDaemonStatusCommand(
        IPamDaemonRepository daemonRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _daemonRepository = daemonRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task SetStatusAsync(Guid organizationId, Guid actingUserId, Guid daemonId, bool enable)
    {
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);
        if (daemon is null || daemon.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var desired = enable ? PamDaemonStatus.Enabled : PamDaemonStatus.Disabled;
        if (daemon.Status == desired)
        {
            throw new BadRequestException(enable
                ? "This daemon is already enabled."
                : "This daemon is already disabled.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // audit (before/after): record the attempt, then the outcome around the status write.
        var audit = new AccessAuditEventData
        {
            Kind = enable ? AccessAuditEventKind.DaemonEnabled : AccessAuditEventKind.DaemonDisabled,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            DaemonId = daemon.Id,
            DaemonName = daemon.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        daemon.Status = desired;
        daemon.RevisionDate = now;
        await _daemonRepository.ReplaceAsync(daemon);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
