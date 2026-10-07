using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IDeleteAccessConnectorCommand" />
public class DeleteAccessConnectorCommand : IDeleteAccessConnectorCommand
{
    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public DeleteAccessConnectorCommand(
        IPamAccessConnectorRepository accessConnectorRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessConnectorRepository = accessConnectorRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task DeleteAsync(Guid organizationId, Guid actingUserId, Guid accessConnectorId)
    {
        var accessConnector = await _accessConnectorRepository.GetByIdAsync(accessConnectorId);
        if (accessConnector is null || accessConnector.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Attempt before the delete, outcome after.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.AccessConnectorDeleted,
            OccurredDate = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            AccessConnectorId = accessConnector.Id,
            AccessConnectorName = accessConnector.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        await _accessConnectorRepository.DeleteAsync(accessConnector);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
