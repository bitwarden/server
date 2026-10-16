using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Bit.Services.Pam.Services;
using Bit.Services.Pam.Utilities;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IAssignAccessConnectorToTargetCommand" />
public class AssignAccessConnectorToTargetCommand : IAssignAccessConnectorToTargetCommand
{
    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IPamRotationConfigRepository _rotationConfigRepository;
    private readonly ICanEditRotationCiphersQuery _canEditRotationCiphersQuery;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public AssignAccessConnectorToTargetCommand(
        IPamAccessConnectorRepository accessConnectorRepository,
        IPamTargetSystemRepository targetSystemRepository,
        IPamRotationConfigRepository rotationConfigRepository,
        ICanEditRotationCiphersQuery canEditRotationCiphersQuery,
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessConnectorRepository = accessConnectorRepository;
        _targetSystemRepository = targetSystemRepository;
        _rotationConfigRepository = rotationConfigRepository;
        _canEditRotationCiphersQuery = canEditRotationCiphersQuery;
        _organizationAbilityCacheService = organizationAbilityCacheService;
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

        await _organizationAbilityCacheService.RequirePamPlanAsync(organizationId);

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

        // The access connector receives every cipher the target system rotates.
        var configs = await _rotationConfigRepository.GetManyDetailsByOrganizationIdAsync(organizationId);
        var cipherIds = configs.Where(c => c.TargetSystemId == targetSystemId).Select(c => c.CipherId);
        if (!await _canEditRotationCiphersQuery.CanEditAllAsync(organizationId, cipherIds))
        {
            throw new BadRequestException("You need edit access to every item this target system rotates.");
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
