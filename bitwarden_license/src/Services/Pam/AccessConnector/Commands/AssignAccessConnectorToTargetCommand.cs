using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IAssignAccessConnectorToTargetCommand" />
public class AssignAccessConnectorToTargetCommand : IAssignAccessConnectorToTargetCommand
{
    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public AssignAccessConnectorToTargetCommand(
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

    public async Task AssignAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId, Guid targetSystemId)
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

        // Both rows matched the route organization, so the same-org invariant holds.
        if (accessConnector.Status != PamAccessConnectorStatus.Enabled)
        {
            throw new BadRequestException("This access connector is deactivated.");
        }

        if (target.Method != PamTargetSystemMethod.Automatic)
        {
            throw new BadRequestException("Only automatic target systems can be assigned an access connector.");
        }

        if (await _accessConnectorRepository.AssignmentExistsAsync(accessConnectorId, targetSystemId))
        {
            throw new BadRequestException("This access connector is already assigned to this target system.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var assignment = new PamAccessConnectorTargetAssignment
        {
            AccessConnectorId = accessConnectorId,
            TargetSystemId = targetSystemId,
            OrganizationId = organizationId,
            CreationDate = now,
        };
        // CreateAssignmentAsync, unlike the generic CreateAsync, does not assign the id.
        assignment.SetNewId();

        // Attempt before the insert, outcome after.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.AccessConnectorAssignedToTarget,
            OccurredDate = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            AccessConnectorId = accessConnector.Id,
            AccessConnectorName = accessConnector.Name,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        await _accessConnectorRepository.CreateAssignmentAsync(assignment);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
