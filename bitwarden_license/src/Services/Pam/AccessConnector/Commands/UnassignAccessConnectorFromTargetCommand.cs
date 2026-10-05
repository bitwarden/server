using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IUnassignAccessConnectorFromTargetCommand" />
public class UnassignAccessConnectorFromTargetCommand : IUnassignAccessConnectorFromTargetCommand
{
    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public UnassignAccessConnectorFromTargetCommand(
        IPamAccessConnectorRepository accessConnectorRepository,
        IPamTargetSystemRepository targetSystemRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessConnectorRepository = accessConnectorRepository;
        _targetSystemRepository = targetSystemRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task UnassignAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId, Guid targetSystemId)
    {
        var accessConnector = await _accessConnectorRepository.GetByIdAsync(accessConnectorId);
        if (accessConnector is null || accessConnector.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var target = await _targetSystemRepository.GetByIdAsync(targetSystemId);
        if (target is null || target.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (!await _accessConnectorRepository.AssignmentExistsAsync(accessConnectorId, targetSystemId))
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.AccessConnectorUnassignedFromTarget,
            OccurredDate = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            AccessConnectorId = accessConnector.Id,
            AccessConnectorName = accessConnector.Name,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        await _accessConnectorRepository.DeleteAssignmentAsync(accessConnectorId, targetSystemId);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
