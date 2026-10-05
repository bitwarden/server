using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="ISetAccessConnectorStatusCommand" />
public class SetAccessConnectorStatusCommand : ISetAccessConnectorStatusCommand
{
    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public SetAccessConnectorStatusCommand(
        IPamAccessConnectorRepository accessConnectorRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessConnectorRepository = accessConnectorRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task SetStatusAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId, bool enable)
    {
        var accessConnector = await _accessConnectorRepository.GetByIdAsync(accessConnectorId);
        if (accessConnector is null || accessConnector.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var desired = enable ? PamAccessConnectorStatus.Enabled : PamAccessConnectorStatus.Disabled;
        if (accessConnector.Status == desired)
        {
            throw new BadRequestException(enable
                ? "This access connector is already active."
                : "This access connector is already deactivated.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // audit (before/after): record the attempt, then the outcome around the status write.
        var audit = new AccessAuditEventData
        {
            Kind = enable ? AccessAuditEventKind.AccessConnectorEnabled : AccessAuditEventKind.AccessConnectorDisabled,
            OccurredDate = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            AccessConnectorId = accessConnector.Id,
            AccessConnectorName = accessConnector.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        accessConnector.Status = desired;
        accessConnector.RevisionDate = now;
        await _accessConnectorRepository.ReplaceAsync(accessConnector);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
